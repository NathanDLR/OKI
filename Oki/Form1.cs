using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace Oki
{
    public partial class frmOki : Form
    {
        // Global Variables to connect to database
        private SqlConnection connection = null;
        private SqlCommand cmd;
        private SqlDataReader reader;
        private String query;

        // Empty xml file
        private XmlDocument xmldoc = new XmlDocument();

        // Files array
        private String[] files;

        public frmOki()
        {
            InitializeComponent();
            stablishConnection();
            loadData();
            lblSetmsg("Presione procesar para procesar los pedidos");            
        }

        // Load XML's files from C:/xml's
        public void loadData()
        {
            // Load files names into listbox
            files = Directory.GetFiles(@"C:\xml's");

            foreach(String file in files)
            {
                // Add file to list
                lstArchivos.Items.Add(Path.GetFileName(file));
            }            
        }

        // Click on btnProcesar
        private void btnProcesar_Click(object sender, EventArgs e)
        {
            importFiles();
        }

        // Click on btnEnviar
        private void btnEnviar_Click(object sender, EventArgs e)
        {
            // Send file to each customer
            sendInfoToCustomers();
        }

        // Set label info message
        private void lblSetmsg(String msg)
        {
            lblTxt.Text = msg;
        }

        // Import xml files
        private void importFiles()
        {
            // Import file to database
            foreach(String file in files)
            {
                try
                {
                    query = "insert into importadosOki(fichero) values ('" + Path.GetFileName(file) + "')";
                    cmd = new SqlCommand(query, connection);
                    cmd.ExecuteNonQuery();

                    readFiles();

                }
                catch(SqlException ex)
                {
                    MessageBox.Show("Error: Los ficheros ya han sido importados " + ex);
                }
                
            }
        }

        // Read files
        private void readFiles()
        {
            foreach(String file in files)
            {
                xmldoc.Load(file);

                // New order for each file
                Order order = new Order();

                foreach (XmlNode node in xmldoc.DocumentElement.ChildNodes)
                {
                    // Get data
                    if (node.HasChildNodes)
                    {
                        for (int i = 0; i < node.ChildNodes.Count; i++)
                        {
                            if (node.ChildNodes[i].HasChildNodes)
                            {
                                for (int j = 0; j < node.ChildNodes[i].ChildNodes.Count; j++)
                                {
                                    // Get ticket id
                                    if (node.ChildNodes[i].ChildNodes[j].Name == "TicketID") order.NumPedido_TicketID = node.ChildNodes[i].ChildNodes[j].InnerText;

                                    // Get submitter
                                    if (node.ChildNodes[i].ChildNodes[j].Name == "Submitter") order.Nomb_Cliente_Submitter = node.ChildNodes[i].ChildNodes[j].InnerText;

                                    // Get XML_Date
                                    if (node.ChildNodes[i].ChildNodes[j].Name == "XML_Date") order.Fecha_XML_Date = node.ChildNodes[i].ChildNodes[j].InnerText;

                                    // Get cp_Cliente_Loc_Post_code, dir_Cliente_Loc_Street and poblacionCliente_LocCity
                                    if (node.ChildNodes[i].ChildNodes[j].Name == "OkiAddressData")
                                    {
                                        order.Cp_Cliente_Loc_Post_code = node.ChildNodes[i].ChildNodes[j].SelectSingleNode("OkiLocation").SelectSingleNode("Loc_Post_code").InnerText;
                                        order.PoblacionCliente_LocCity = node.ChildNodes[i].ChildNodes[j].SelectSingleNode("OkiLocation").SelectSingleNode("Loc_City").InnerText;
                                        order.Dir_Cliente_Loc_Street = node.ChildNodes[i].ChildNodes[j].SelectSingleNode("OkiLocation").SelectSingleNode("Loc_Street").InnerText;
                                    }

                                    // Get Warranty_ArticleNumber, Warranty_ArticleDescription and Warranty_Serialnumber
                                    if (node.ChildNodes[i].ChildNodes[j].Name == "OkiEquipmentData")
                                    {
                                        order.Codigo_Warranty_Articlenumber = node.ChildNodes[i].ChildNodes[j].SelectSingleNode("Warranty_Articlenumber").InnerText;
                                        order.Descrip_Warranty_ArticleDescription = node.ChildNodes[i].ChildNodes[j].SelectSingleNode("Warranty_ArticleDescription").InnerText;
                                        order.Serie_Warranty_SerialNumber = node.ChildNodes[i].ChildNodes[j].SelectSingleNode("Warranty_Serialnumber").InnerText;

                                    }

                                    // Get articles data
                                    if (node.ChildNodes[i].ChildNodes[j].Name == "OkiWarrantyExchange")
                                    {
                                        // Get data
                                        foreach (XmlNode childNode in node.ChildNodes[i].ChildNodes[j].ChildNodes)
                                        {
                                            Article article = new Article();

                                            // Article number
                                            article.ArticleNumber = childNode.SelectSingleNode("ArticleNumber").InnerText;

                                            // Article description
                                            article.Desc = childNode.SelectSingleNode("ArticleDescription").InnerText;

                                            // Warehouse
                                            article.Warehouse = "OKI";

                                            // Location 
                                            article.Location = "";

                                            // Add article to articles order list
                                            order.Articles.Add(article);
                                        }
                                    }

                                    // Get Workcenter
                                    if (node.ChildNodes[i].ChildNodes[j].Name == "OkiServiceFees") order.Workcenter = node.ChildNodes[i].ChildNodes[j].SelectSingleNode("WorkC_Work_Center").InnerText;

                                }
                            }
                        }
                    }

                    // Send order to checkStock method to check wether ther's available stock or not
                    checkStock(order);
                }        
            }            
        }

        // Check stock
        private void checkStock(Order order)
        {
            // Iterate through articles and check each one
            for(int i = 0; i < order.Articles.Count; i++)
            {
                Boolean stockAtWarehouse = true;
                int savedQty = 0;

                // Check warehouse stock using Table Existencias from DataBase
                try
                {
                    query = "select * from existencias where codigo = '" + order.Articles[i].ArticleNumber + "'";
                    cmd = new SqlCommand(query, connection);
                    reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        // Check stock 
                        if (Convert.ToInt32(reader["cant_existente2"].ToString()) > 0)
                        {
                            // Save stock to send it
                            // Check saved qty and update it 
                            savedQty = Convert.ToInt32(reader["cant_reservada"].ToString());
                            savedQty++;

                            stockAtWarehouse = true;
                        }

                        // If there's no available stock, create an order
                        else
                        {
                            stockAtWarehouse = false;
                        }
                    }

                    // Add it to pedido_suministros_cabeceras and pedido_suministros_detalle
                }
                catch (SqlException ex)
                {
                    MessageBox.Show("Error: " + ex);
                }

                reader.Close();

                if (stockAtWarehouse) saveStock(savedQty, order.Articles[i].ArticleNumber);
                else createOrder(order, order.Articles[i]);

            }
        }

        // Save stock
        private void saveStock(int savedQty, String articleNumber)
        {
            // Update database 
            query = $"update Existencias set cant_reservada = {savedQty} where codigo = '{articleNumber}'";
            cmd = new SqlCommand(query, connection);
            cmd.ExecuteNonQuery();
        }

        // Create Order
        private void createOrder(Order order, Article article)
        {
            String num_pedido = order.NumPedido_TicketID; // Order number
            String codigo = article.ArticleNumber; // Article number
            String descripcion = article.Desc; // Article description
            int cantidad = 1; // Quantity
            int numAlbaran = 1; // Número de Albarán
            int PdtEntregar = 1; 
            int n_cliente = 

            // Create new order and insert it into pedidos_compra


        }

        // Send Info to customers
        private void sendInfoToCustomers()
        {

        }

        // Class Pedido
        class Order
        {
            // Articles list
            private List<Article> articles = new List<Article>();

            // Order data
            private String numPedido_TicketID;
            private String codigo_Warranty_Articlenumber;
            private String descrip_Warranty_ArticleDescription;
            private String serie_Warranty_SerialNumber;
            private String workcenter;
            private String cp_Cliente_Loc_Post_code;
            private String poblacionCliente_LocCity;
            private String nomb_Cliente_Submitter;
            private String dir_Cliente_Loc_Street;
            private String fecha_XML_Date;

            // txt Files
            private String WorkCenterFileRoute;
            private String BuyOrderFileRoute;
            private String ProcessFileRoute;

            // Constructor
            public Order() { }

            // Getters and Setters
            public string NumPedido_TicketID { get => numPedido_TicketID; set => numPedido_TicketID = value; }
            public string Codigo_Warranty_Articlenumber { get => codigo_Warranty_Articlenumber; set => codigo_Warranty_Articlenumber = value; }
            public string Descrip_Warranty_ArticleDescription { get => descrip_Warranty_ArticleDescription; set => descrip_Warranty_ArticleDescription = value; }
            public string Serie_Warranty_SerialNumber { get => serie_Warranty_SerialNumber; set => serie_Warranty_SerialNumber = value; }
            public string Workcenter { get => workcenter; set => workcenter = value; }
            public string Cp_Cliente_Loc_Post_code { get => cp_Cliente_Loc_Post_code; set => cp_Cliente_Loc_Post_code = value; }
            public string PoblacionCliente_LocCity { get => poblacionCliente_LocCity; set => poblacionCliente_LocCity = value; }
            public string Nomb_Cliente_Submitter { get => nomb_Cliente_Submitter; set => nomb_Cliente_Submitter = value; }
            public string Dir_Cliente_Loc_Street { get => dir_Cliente_Loc_Street; set => dir_Cliente_Loc_Street = value; }
            public string Fecha_XML_Date { get => fecha_XML_Date; set => fecha_XML_Date = value; }
            public string WorkCenterFileRoute1 { get => WorkCenterFileRoute; set => WorkCenterFileRoute = value; }
            public string BuyOrderFileRoute1 { get => BuyOrderFileRoute; set => BuyOrderFileRoute = value; }
            public string ProcessFileRoute1 { get => ProcessFileRoute; set => ProcessFileRoute = value; }
            public List<Article> Articles { get => articles; set => articles = value; }
        }

        // Class Article
        class Article
        {
            // Attributes & Properties
            private String articleNumber;
            private String serialNumber;
            private String desc;
            private String warehouse;
            private String location;

            // Constructors
            public Article() { }

            public Article(String articleNumber, String desc, String warehouse, String location)
            {
                this.ArticleNumber = articleNumber;
                this.Desc = desc;
                this.Warehouse = warehouse;
                this.Location = location;
            }

            public string ArticleNumber { get => articleNumber; set => articleNumber = value; }
            public string SerialNumber { get => serialNumber; set => serialNumber = value; }
            public string Desc { get => desc; set => desc = value; }
            public string Warehouse { get => warehouse; set => warehouse = value; }
            public string Location { get => location; set => location = value; }
        }

        public void stablishConnection()
        {
            lblSetmsg("Connecting to Database...");

            try
            {
                // string cadenaDeConexion = @"Server=DESKTOP-ETLPSC5; Database=OkiSpain; Integrated Security=true;";
                string cadenaDeConexion = @"Server=NATE; Database=OkiSpain; Integrated Security=true;";

                connection = new SqlConnection(cadenaDeConexion);

                connection.Open();
                lblSetmsg("Success connecting to database!");

            }
            catch (SqlException ex)
            {
                MessageBox.Show("Problema al tratar de conectar a BD. Detalles:" + ex);
            }
        }
    }
}
