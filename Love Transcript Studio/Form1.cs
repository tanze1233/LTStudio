using DevExpress.XtraBars;
using DevExpress.XtraBars.Navigation;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Love_Transcript_Studio
{
    public partial class Form1 : DevExpress.XtraBars.FluentDesignSystem.FluentDesignForm
    {
        private readonly SqlConnection connection = new SqlConnection("Data Source=81.68.224.133,59742;Initial Catalog=0099Data;Persist Security Info=True;User ID=jane0099;Password=JaneJack@10;Encrypt=False;Trust Server Certificate=True;");

        protected override bool ExtendNavigationControlToFormTitle
        {
            get { return true; }
        }

        public Form1()
        {
            InitializeComponent();
            CreateAppFolder();

            //connection.Open();
            Debug.WriteLine("DB Connected.");
            GetAllProjects();
        }

        private void CreateAppFolder()
        {
            try
            {
                var docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                System.IO.Directory.CreateDirectory($"{docPath}\\LoveTranscriptStudio\\Projects\\Audios");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error: {e}");
                MessageBox.Show($"Cannot prepare app storage! \nError: {e}", "ERROR");
            }
        }

        private void GetAllProjects()
        {
            accordionControlElement1.Elements.Clear();
            connection.Open();
            string sqlStr = "select * from dbo.Projects;";
            // 执行SQL语句的对象
            SqlCommand Mycom = new SqlCommand(sqlStr, connection);  // 参数1：查询语句； 参数2：连接对象，必须要打开状态

            // 接收结果
            SqlDataReader checkstr = Mycom.ExecuteReader();   // 只能查询

            while (checkstr.Read())
            {
                Debug.WriteLine($"{checkstr["ID"]}-Name: {checkstr["Name"]}-BaseDir: {checkstr["BaseDir"]}");   // 读取数据
                //accordionControl1.Elements.Add(new AccordionControlElement(ElementStyle.Item) { Name = checkstr["Name"].ToString(), Text = checkstr["Name"].ToString() });
                accordionControlElement1.Elements.Add(new AccordionControlElement(ElementStyle.Item) { Name = checkstr["Name"].ToString(), Text = checkstr["Name"].ToString(), Tag = checkstr["ID"] });
            }
            connection.Close();   // 关闭数据库
        }

        private void GetProjectInfo(int ProjectId)
        {
            try
            {
                labelControl5.Text = "";
                connection.Open();
                string sqlStr = $"select * from dbo.Projects where ID = {ProjectId};";
                // 执行SQL语句的对象
                SqlCommand Mycom = new SqlCommand(sqlStr, connection);  // 参数1：查询语句； 参数2：连接对象，必须要打开状态

                // 接收结果
                SqlDataReader checkstr = Mycom.ExecuteReader();   // 只能查询

                if (checkstr.HasRows)
                {
                    while (checkstr.Read())
                    {
                        Debug.WriteLine($"{checkstr["ID"]}-Name: {checkstr["Name"]}-BaseDir: {checkstr["BaseDir"]}-Status: {checkstr["Ready"]}");   // 读取数据
                        labelControl4.Text = checkstr["Name"].ToString();
                        //Debug.WriteLine(checkstr["Ready"].GetType());
                        labelControl8.Text = checkstr.GetByte(checkstr.GetOrdinal("Ready")) == 0 ? "Project Not Ready" : "Ready to Open";
                        labelControl8.ForeColor = checkstr.GetByte(checkstr.GetOrdinal("Ready")) == 0 ? Color.IndianRed : Color.ForestGreen;
                        labelControl7.Text = checkstr.GetByte(checkstr.GetOrdinal("Ready")) == 0 ? "Click \"Prepare Project\" Button to Get Ready." : "You can open this project now.";
                        var filesJson = checkstr["BaseDir"].ToString();
                        var filesList = JsonConvert.DeserializeObject<List<string>>(filesJson);
                        foreach (var file in filesList) { labelControl5.Text += $"{file}\n"; }
                    }
                }
                connection.Close();
            }
            catch (Exception e)
            {
                connection.Close();
                Debug.WriteLine(e.Message);
                labelControl8.Text = "Cannot Load Project";
                labelControl8.ForeColor = Color.IndianRed;
                labelControl7.Text = e.Message;
            }


        }

        private void accordionControl1_ElementClick(object sender, DevExpress.XtraBars.Navigation.ElementClickEventArgs e)
        {
            if (e.Element.Style == DevExpress.XtraBars.Navigation.ElementStyle.Group) return;
            if (e.Element.Tag == null) return;
            int itemID = (int)e.Element.Tag;
            Debug.WriteLine($"Project ID {itemID} Clicked!");
            GetProjectInfo(itemID);
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            flyoutPanel1.ShowPopup();
        }

        private void simpleButton3_Click(object sender, EventArgs e)
        {
            flyoutPanel1.HidePopup();
        }

        private void hyperlinkLabelControl1_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "Audio files|*.mp3;*.wav;*.m4a;*.mp4;*.mov;*.avi";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                //listBoxControl1.Items.Clear();
                foreach (var fileitem in openFileDialog1.FileNames)
                {
                    listBoxControl1.Items.Add(fileitem);
                }
            }
            simpleButton2.Enabled = listBoxControl1.Items.Count > 0;
        }

        private void hyperlinkLabelControl2_Click(object sender, EventArgs e)
        {
            listBoxControl1.Items.Clear();
            simpleButton2.Enabled = false;
        }

        private void simpleButton2_Click(object sender, EventArgs e)
        {
            if (textEdit1.Text.Length > 0 && listBoxControl1.Items.Count > 0)
            {
                List<string> fileStrings = new List<string>();
                foreach (var item in listBoxControl1.Items)
                {
                    fileStrings.Add(item.ToString());
                }

                CopyAndTranscodeAudioFiles(fileStrings);

                var fileJsonString = JsonConvert.SerializeObject(fileStrings);
                if (PostNewProjectToDatabase(textEdit1.Text, fileJsonString))
                {
                    flyoutPanel1.HidePopup();
                    GetAllProjects();
                }
                else
                {
                    flyoutPanel1.HidePopup();
                    alertControl1.Show(Owner, "Save Error", "Cannot create new project!");
                }
            }
            else
            {
                alertControl1.Show(Owner, "Missing Information", "Name and Audio Files must not be empty!");
            }
        }

        private void textEdit1_TextChanged(object sender, EventArgs e)
        {
            if (textEdit1.Text.Length > 0 && listBoxControl1.Items.Count > 0)
            {
                simpleButton2.Enabled = true;
            }
            else
            {
                simpleButton2.Enabled = false;
            }
        }

        private void CopyAndTranscodeAudioFiles(List<string> files)
        {
            try
            {
                var docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var destFolder = $"{docPath}\\LoveTranscriptStudio\\Projects\\Audios";
                foreach (var item in files)
                {
                    FileInfo fi = new FileInfo(item);
                    var destFile = Path.Combine(destFolder, $"{Guid.NewGuid()}{fi.Extension}");
                    File.Copy(item, destFile, false);
                }
            }
            catch (Exception)
            {

                throw;
            }
        }

        private bool PostNewProjectToDatabase(string name, string files)
        {
            SqlParameter parameter;
            connection.Open();

            try
            {
                using (var command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandType = CommandType.Text;
                    command.CommandText = @"  
INSERT INTO dbo.Projects  
		(Name,  
		BaseDir  
		)  
	VALUES  
		(@Name,  
		@BaseDir  
		); ";

                    parameter = new SqlParameter("@Name", SqlDbType.NVarChar, 255)
                    {
                        Value = name
                    };
                    command.Parameters.Add(parameter);

                    parameter = new SqlParameter("@BaseDir", SqlDbType.NVarChar, -1)
                    {
                        Value = files
                    };
                    command.Parameters.Add(parameter);

                    command.ExecuteScalar();
                }
                connection.Close();
                return true;
            }
            catch (Exception e)
            {
                connection.Close();
                Debug.WriteLine($"Error: {e.Message}");
                return false;
            }


        }

        private void barHeaderItem1_ItemClick(object sender, ItemClickEventArgs e)
        {

        }
    }
}
