using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
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
        // Empty xml file
        XmlDocument xmldoc = new XmlDocument();

        // Files array
        private String[] files;

        public frmOki()
        {
            InitializeComponent();
            loadData();
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

        }

        // Click ob btnEnviar
        private void button2_Click(object sender, EventArgs e)
        {

        }
    }
}
