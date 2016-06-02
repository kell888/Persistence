using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using KellPersistence;
using System.IO;

namespace Test
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        Trunk<MyData> trunk;
        TrunkInfo<Data<MyData>> curr;
        
        private void Form1_Load(object sender, EventArgs e)
        {
            Form2 f = new Form2();
            if (f.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBox5.Text = f.TableName;
                LoadTable();
            }
        }

        private void LoadTable()
        {
            comboBox1.Items.Clear();
            string table = textBox5.Text;
            trunk = new Trunk<MyData>(table);
            List<Data<MyData>> all = trunk.SelectAll();
            foreach (Data<MyData> item in all)
            {
                TrunkInfo<Data<MyData>> tr = new TrunkInfo<Data<MyData>>(table, Data<MyData>.Parse(item.Trunk, item.ID, item.Buffer, item.Header.User));
                comboBox1.Items.Add(tr);
            }
            if (comboBox1.Items.Count > 0)
            {
                comboBox1.SelectedIndex = 0;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //添加
            string table = textBox5.Text.Trim();
            if (!trunk.Name.Equals(table, StringComparison.InvariantCultureIgnoreCase))
                trunk.Rename(table);
            Data<MyData> current = new Data<MyData>(trunk, new MyData(textBox1.Text), numericUpDown1.Value.ToString());
            if (trunk.Insert(current))
            {
                curr = new TrunkInfo<Data<MyData>>(trunk.Name, Data<MyData>.Parse(trunk, current.ID, current.Buffer, current.Header.User));
                comboBox1.Items.Add(curr);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            //修改
            string table = textBox5.Text.Trim();
            if (!trunk.Name.Equals(table, StringComparison.InvariantCultureIgnoreCase))
            {
                trunk.Rename(table);
                curr.Name = table;
            }
            Data<MyData> current = Data<MyData>.Parse(trunk, curr.Data.ID, new MyData(curr.Data.ID, curr.Data.Header, textBox1.Text), curr.Data.Header.User);
            curr.Data.Update(trunk.Identity, numericUpDown1.Value.ToString(), current.Buffer);
            if (trunk.Update(current))
            {
                UpdateList(curr);
            }
        }

        private void UpdateList(TrunkInfo<Data<MyData>> trunkInfo)
        {
            for (int i = 0; i < comboBox1.Items.Count; i++)
            {
                TrunkInfo<Data<MyData>> tr = comboBox1.Items[i] as TrunkInfo<Data<MyData>>;
                if (tr != null && tr.Name == trunkInfo.Name && tr.Data.ID == trunkInfo.Data.ID)
                {
                    tr.Data.Update(trunk.Identity, trunkInfo.Data.Header.User, trunkInfo.Data.Buffer);
                }
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            //删除
            string table = textBox5.Text.Trim();
            if (!trunk.Name.Equals(table, StringComparison.InvariantCultureIgnoreCase))
                trunk.Rename(table);
            if (trunk.Delete(curr.Data.ID))
            {
                DeleteFromList(curr);
                curr = null;
                textBox1.Clear();
            }
        }

        private void DeleteFromList(TrunkInfo<Data<MyData>> trunkInfo)
        {
            for (int i = comboBox1.Items.Count - 1; i > -1; i--)
            {
                TrunkInfo<Data<MyData>> tr = comboBox1.Items[i] as TrunkInfo<Data<MyData>>;
                if (tr != null && tr.Name == trunkInfo.Name && tr.Data.ID == trunkInfo.Data.ID)
                {
                    comboBox1.Items.RemoveAt(i);
                }
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            //清空
            string table = textBox5.Text.Trim();
            if (!trunk.Name.Equals(table, StringComparison.InvariantCultureIgnoreCase))
                trunk.Rename(table);
            if (trunk.Truncate())
            {
                comboBox1.Items.Clear();
                curr = null;
                textBox1.Clear();
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            //读取
            if (comboBox1.SelectedIndex > -1)
            {
                TrunkInfo<Data<MyData>> selected = comboBox1.SelectedItem as TrunkInfo<Data<MyData>>;
                if (selected != null)
                {
                    string table = textBox5.Text.Trim();
                    if (!trunk.Name.Equals(table, StringComparison.InvariantCultureIgnoreCase))
                        trunk.Rename(table);
                    Data<MyData> data = trunk.Select(selected.Data.ID);
                    if (data != null)
                    {
                        GetSelectDataInfo(trunk.Name, data.ID);
                        textBox1.Text = data.Buffer.Str;
                        textBox4.Text = data.Header.ToString();
                    }
                }
            }
        }

        private void GetSelectDataInfo(string table, Guid id)
        {
            foreach (object o in comboBox1.Items)
            {
                TrunkInfo<Data<MyData>> tr = o as TrunkInfo<Data<MyData>>;
                if (tr != null && tr.Name == table && tr.Data.ID == id)
                {
                    curr = tr;
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string table = textBox5.Text.Trim();
            if (!trunk.Name.Equals(table, StringComparison.InvariantCultureIgnoreCase))
                trunk.Rename(table);
            if (trunk.Drop())
            {
                comboBox1.Items.Clear();
                curr = null;
                textBox1.Clear();
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            //查询
            string keyword = textBox2.Text;
            CustomQueryProvider<MyData> provider = new CustomQueryProvider<MyData>();
            provider.Trunk = trunk;
            //Lambda表达式
            //var list = provider.Where<MyData>(a => a.Str.Contains(keyword));
            //Linq表达式
            var list = from item in provider
                       where item.Str.Contains(keyword)
                       select item;
            //string s = provider.AnalysisExpression(provider.MyExpression);
            //Console.WriteLine("-- " + s + " --");
            //IQueryable record = DataProviderContext.Execute(list.Expression, true, trunk) as IQueryable;
            //IQueryable<MyData> result = record as IQueryable<MyData>;
            //list.Provider.Execute<MyData>(list.Expression);//立即执行
            //IQueryable<MyData> result = list.AsQueryable<MyData>();
            listBox1.Items.Clear();
            foreach (var data in list)
            {//延迟执行
                listBox1.Items.Add(data);
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.RootFolder = Environment.SpecialFolder.Desktop;
            folderBrowserDialog1.ShowNewFolderButton=true;
            folderBrowserDialog1.Description = "请指定要另存的目录，这是个覆盖的危险动作，请注意！";
            if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string table = textBox5.Text.Trim();
                if (!trunk.Name.Equals(table, StringComparison.InvariantCultureIgnoreCase))
                    trunk.Rename(table);
                if (trunk.Bulk(folderBrowserDialog1.SelectedPath))
                    MessageBox.Show("另存完毕！");
                else
                    MessageBox.Show("另存失败！");
            }
            folderBrowserDialog1.Dispose();
        }

        private void button8_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.RootFolder = Environment.SpecialFolder.Desktop;
            folderBrowserDialog1.Description = "请指定要载入的目录，这是个覆盖的危险动作，请注意！";
            if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string table = textBox5.Text.Trim();
                if (!trunk.Name.Equals(table, StringComparison.InvariantCultureIgnoreCase))
                    trunk.Rename(table);
                if (trunk.Bulk(folderBrowserDialog1.SelectedPath, true))
                {
                    textBox5.Text = trunk.Name;
                    LoadTable();
                    MessageBox.Show("载入完毕！");
                }
                else
                {
                    MessageBox.Show("载入失败！");
                }
            }
            folderBrowserDialog1.Dispose();
        }

        private void button9_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.RootFolder = Environment.SpecialFolder.Desktop;
            folderBrowserDialog1.Description = "请指定要压缩的表目录";
            if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                //saveFileDialog1.Filter = "压缩文件(*.zip)|*.zip";
                //saveFileDialog1.Title = "请输入表的压缩包名";
                //saveFileDialog1.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                //if (saveFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                //{
                    string table = textBox5.Text.Trim();
                    if (!trunk.Name.Equals(table, StringComparison.InvariantCultureIgnoreCase))
                        trunk.Rename(table);
                    if (Trunk.ZipTable(folderBrowserDialog1.SelectedPath))//, saveFileDialog1.FileName))
                        MessageBox.Show("压缩完毕！");
                    else
                        MessageBox.Show("压缩失败！");
                //}
                //saveFileDialog1.Dispose();
            }
            folderBrowserDialog1.Dispose();
        }

        private void button10_Click(object sender, EventArgs e)
        {
            //folderBrowserDialog1.RootFolder = Environment.SpecialFolder.Desktop;
            //folderBrowserDialog1.Description = "请指定要解压的表目录";
            //if (folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            //{
                openFileDialog1.Filter = "压缩文件(*.zip)|*.zip";
                openFileDialog1.Title = "请选定要解压后的表压缩包";
                openFileDialog1.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string table = textBox5.Text.Trim();
                    if (!trunk.Name.Equals(table, StringComparison.InvariantCultureIgnoreCase))
                        trunk.Rename(table);
                    if (Trunk.UnzipTable(openFileDialog1.FileName))//, folderBrowserDialog1.SelectedPath))
                        MessageBox.Show("解压成功！");
                    else
                        MessageBox.Show("解压失败！");
                }
                openFileDialog1.Dispose();
            //}
            //folderBrowserDialog1.Dispose();
        }
    }
}
