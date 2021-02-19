using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
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
            btnProcesar.Enabled = false;
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
                    
                }                
            }
        }

        // Read files
        private void readFiles()
        {
            foreach(String file in files)
            {
                /*
                string sourcePath = @"C:\xml's";
                string targetPath = @"C:\xml's/importado";

                string sourceFile = Path.Combine(sourcePath, Path.GetFileName(file));
                string destFile = Path.Combine(targetPath, Path.GetFileName(file));
                */

                xmldoc.Load(file);

                // New order for each file
                Order order = new Order();

                // Get file name and store it in observaciones
                order.Observaciones = Path.GetFileName(file);

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
                                    order.Observaciones += $"{order.Workcenter}";
                                }
                            }
                        }
                    }

                    // Send order to checkStock method to check wether ther's available stock or not
                    checkStock(order);

                    // Remove file from list
                    if (lstArchivos.Items.Count > 0)
                    {
                        lstArchivos.Items.RemoveAt(0);
                        // File.Move(sourceFile, destFile);
                    }
                    

                }        
            }

            // Update lblText
            lblSetmsg("Presione Enviar para enviar los\ninformes de los archivos procesados");
        }

        // Check stock
        private void checkStock(Order order)
        {
            Boolean cabeceraCreated = false;

            // Iterate through articles and check each one
            for (int i = 0; i < order.Articles.Count; i++)
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

                }
                catch (SqlException ex)
                {
                    MessageBox.Show("Error: " + ex);
                }

                reader.Close();

                if (stockAtWarehouse) saveStock(savedQty, order.Articles[i].ArticleNumber);
                else
                {
                    createOrder(order, order.Articles[i], cabeceraCreated);
                    cabeceraCreated = true;
                }

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
        private void createOrder(Order order, Article article, Boolean cabeceraCreated)
        {
            Client client = new Client();

            String num_pedido = order.NumPedido_TicketID; // Order number
            String codigo = article.ArticleNumber; // Article number
            String descripcion = article.Desc; // Article description
            String workcenter = order.Workcenter; // Client's workcenter
            int cantidad = 1; // Quantity
            int numAlbaran = 1; // Número de Albarán
            int PdtEntregar = 1; // Pendiente de entregar

            // We'll get n_cliente from Table Clientes using client's workcenter
            query = $"select top 1 * from Clientes where workcentre = '{workcenter}'";
            cmd = new SqlCommand(query, connection);
            reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                // Get client data that we'll need later 
                client.N_cliente = Convert.ToInt32(reader["n_cliente"].ToString());
                client.Nom_cliente = reader["NombreCli"].ToString();
                client.Dir_cliente = reader["Domicilio"].ToString();
                client.Cp_cliente = reader["CodPostal"].ToString();
                client.Poblacion_cliente = reader["Población"].ToString();
                client.Provincia_cliente = reader["Provincia"].ToString();
            }

            // Close reader
            reader.Close();

            // Insert data into pedidos_suministros_cabecera
            // This data has to be inserted just once, so we'll use a boolean to do it
            if(cabeceraCreated == false)
            {
                query = "insert into pedidos_suministros_cabecera(Num_pedido, Fecha, n_cliente, serie, nom_cliente, dir_cliente, cp_cliente, poblacion_cliente, provincia_cliente, workc) " +
                    $"values('{num_pedido}', '{order.Fecha_XML_Date}', {client.N_cliente}, '{order.Serie_Warranty_SerialNumber}', '{client.Nom_cliente}', '{client.Dir_cliente}',"
                    + $" '{client.Cp_cliente}' ,'{client.Poblacion_cliente}', '{client.Provincia_cliente}', '{workcenter}'" + ")";
                cmd = new SqlCommand(query, connection);
                cmd.ExecuteNonQuery();
            }

            // Insert data into pedidos_suministros_detalles
            query = "insert into pedidos_suministros_detalle(Num_pedido, Codigo, Descripcion, Cantidad, PendienteEnviar, cant_pedida, cant_a_pedir, cant_sinstock, pedido, envioacliente, n_cliente, cant_reservada, nserie, observaciones) " +
                $"values('{num_pedido}', '{codigo}', '{descripcion}', 1, 0, 0, 0, 0, 1, 1, '{client.N_cliente}', 1, '{order.Serie_Warranty_SerialNumber}', '{order.Observaciones}')";
            cmd = new SqlCommand(query, connection);
            cmd.ExecuteNonQuery();
            
            // Insert data into pedidos_compra
            query = "insert into pedidos_compra(Num_pedido, Codigo, Descripcion, Cantidad, numAlbaran, importado, entregada, PdtEntregar, n_cliente, cant_original)" +
                $" values('{num_pedido}', '{codigo}', '{descripcion}', 1, 1, 0, 0, 1, '{client.N_cliente}', 0)";
            cmd = new SqlCommand(query, connection);
            cmd.ExecuteNonQuery();
        }

        // Send Info to customers
        private void sendInfoToCustomers()
        {
            // Archivo para los talleres NO IMPLEMENTADO
            infoTalleres();

            // Archivo pedido de compra con el material que falta
            pedidoCompra();

            // Archivo con los pedidos procesados
            pedidosProcesados();

            // Enviar archivos
            sendFiles();
        }

        // Mandar la info a los diferentes talleres que han hecho los pedidos
        private void infoTalleres()
        {

        }

        // Mandar el pedido de compra con el material que hace falta pedir
        private void pedidoCompra()
        {
            StreamWriter file = File.CreateText(@"C:\ficheros/pedidos.txt");

            file.WriteLine("Lista de los materiales que faltan");

            query = "select * from pedidos_compra";
            cmd = new SqlCommand(query, connection);
            reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                file.WriteLine("---------------------------------------------------------");
                file.WriteLine($"Número del pedido: {reader["Num_pedido"].ToString()}");
                file.WriteLine($"Código: {reader["Codigo"].ToString()}");
                file.WriteLine($"Descripción: {reader["Descripcion"].ToString()}");
                file.WriteLine($"Cantidad: {reader["Cantidad"].ToString()}");
                file.WriteLine($"Cantidad pendiente de entregar: {reader["PdtEntregar"].ToString()}");
                file.WriteLine($"Número del cliente: {reader["n_cliente"].ToString()}");
                file.WriteLine("---------------------------------------------------------");

            }

            reader.Close();
            file.Close();
        }

        // Mandar los pedidos que han sido procesados
        private void pedidosProcesados()
        {
            StreamWriter file = File.CreateText(@"C:\ficheros/procesado.txt");

            file.WriteLine("Lista de los pedidos procesados");

            query = "select * from pedidos_suministros_detalle";
            cmd = new SqlCommand(query, connection);
            reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                file.WriteLine("---------------------------------------------------------");
                file.WriteLine($"Número del pedido: {reader["Num_pedido"].ToString()}");
                file.WriteLine($"Código: {reader["Codigo"].ToString()}");
                file.WriteLine($"Número de serie: {reader["nserie"].ToString()}");
                file.WriteLine($"Descripción: {reader["Descripcion"].ToString()}");
                file.WriteLine($"Cantidad: {reader["Cantidad"].ToString()}");
                file.WriteLine($"Número del cliente: {reader["n_cliente"].ToString()}");
                file.WriteLine("---------------------------------------------------------");
            }

            reader.Close();
            file.Close();

        }

        // Mandar los archivos por correo
        private void sendFiles()
        {
            // Global variables to send info via email
            String remitente = "alumnos2damAlmunia@gmail.com";
            String destinatario = "natandelreal@gmail.com";
            String password = "2dam2dam";
            String asunto = "Pedidos OKI - Nathan";
            String mensaje = "Ficheros con el pedido de compra y los pedidos procesados";

            MailMessage mail = new MailMessage();
            mail.From = new MailAddress(remitente);
            mail.To.Add(remitente);
            mail.Subject = asunto;
            mail.Body = mensaje;

            System.Net.Mail.Attachment pedidos, procesados;
            pedidos = new System.Net.Mail.Attachment(@"C:\ficheros/pedidos.txt");
            procesados = new System.Net.Mail.Attachment(@"C:\ficheros/procesado.txt");
            mail.Attachments.Add(pedidos);
            mail.Attachments.Add(procesados);

            SmtpClient client = new SmtpClient()
            {
                Host = "smtp.gmail.com",
                Port = 587,
                Credentials = new NetworkCredential(remitente, password),
                EnableSsl = true
            };

            try
            {
                client.Send(mail);
            }
            catch(Exception ex)
            {
                MessageBox.Show("Ha habido un error al mandar los archivos: " + ex);
            }
        }

        // Class Order
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
            private String observaciones;

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
            public string Observaciones { get => observaciones; set => observaciones = value; }
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

        // Class Cliente
        class Client
        {
            // Attributes & Properties
            private int n_cliente;
            private String nom_cliente;
            private String dir_cliente;
            private String cp_cliente;
            private String poblacion_cliente;
            private String provincia_cliente;

            public int N_cliente { get => n_cliente; set => n_cliente = value; }
            public string Nom_cliente { get => nom_cliente; set => nom_cliente = value; }
            public string Dir_cliente { get => dir_cliente; set => dir_cliente = value; }
            public string Cp_cliente { get => cp_cliente; set => cp_cliente = value; }
            public string Poblacion_cliente { get => poblacion_cliente; set => poblacion_cliente = value; }
            public string Provincia_cliente { get => provincia_cliente; set => provincia_cliente = value; }
        }

        public void stablishConnection()
        {
            lblSetmsg("Connecting to Database...");

            try
            {
                string cadenaDeConexion = @"Server=DESKTOP-ETLPSC5; Database=OkiSpain; Integrated Security=true;";
                // string cadenaDeConexion = @"Server=NATE; Database=OkiSpain; Integrated Security=true;";

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
