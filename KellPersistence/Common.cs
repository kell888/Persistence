using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;

namespace KellPersistence
{
    public static class Common
    {
        public const int MaxDirLength = 244 - 37;//减去/{GUID}.KPF=1+32+4=37长度
        public const int MaxFileLength = 260 - 37;//减去/{GUID}.KPF=1+32+4=37长度
        /// <summary>
        /// 如果在配置文档里设置了path配置项，就按path路径保存数据(如果配置的路径存在的话)，否则就在当前应用程序的根目录下
        /// </summary>
        public static string CurrentPath
        {
            get
            {
                string path = ConfigurationManager.AppSettings["path"];
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.EndsWith("\\"))
                    {
                        path = path.TrimEnd('\\');
                        if (Directory.Exists(path))
                            return path;
                    }
                    else
                    {
                        if (Directory.Exists(path))
                            return path;
                    }
                }
                string p = AppDomain.CurrentDomain.BaseDirectory;
                return p.Substring(0, p.Length - 1);
            }
        }

        /// <summary>
        /// 检查文件名是否合法
        /// </summary>
        /// <param name="fileName">文件名,不包含路径</param>
        /// <returns></returns>
        public static bool IsValidFileName(string fileName)
        {
            bool isValid = true;
            string errChar = "\\/:*?\"<>|";
            if (string.IsNullOrEmpty(fileName))
                return false;

            for (int i = 0; i < errChar.Length; i++)
            {
                if (fileName.Contains(errChar[i].ToString()))
                {
                    isValid = false;
                    break;
                }
            }
            return isValid;
        }

        public static byte[] GetBytes(Data data)
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            bf.Serialize(ms, data);
            return ms.ToArray();
        }

        public static Data GetData(byte[] bytes)
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(bytes);
            Data data = (Data)bf.Deserialize(ms);
            return data;
        }

        public static byte[] GetBytes(DataHeader header)
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            bf.Serialize(ms, header);
            return ms.ToArray();
        }

        public static DataHeader GetDataHeader(byte[] bytes)
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(bytes);
            DataHeader header = (DataHeader)bf.Deserialize(ms);
            return header;
        }

        public static byte[] GetBytes(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        public static string GetString(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        public static byte[] GetBytes<T>(T obj)
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            bf.Serialize(ms, obj);
            return ms.ToArray();
        }

        public static T GetObject<T>(byte[] bytes)
        {
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(bytes);
            try
            {//这里可能会因为重新更新了早期的数据类型，造成不兼容而产生异常，故做异常捕获处理
                T obj = (T)bf.Deserialize(ms);
                return obj;
            }
            catch
            {
                return default(T);
            }
        }

        public static void Zip(string zipfilepath, string sourceDir, bool recurse = false)
        {
            try
            {
                FastZip zip = new FastZip();
                zip.CreateZip(zipfilepath, sourceDir, recurse, "^.*?(?<!.zip)$", "");
            }
            catch (ZipException e)
            {
                throw e;
            }
        }

        public static void Unzip(string zipfilepath, string destDir)
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(zipfilepath);
                string targetDir = destDir + "\\" + name;
                FastZip zip = new FastZip();
                zip.ExtractZip(zipfilepath, targetDir, FastZip.Overwrite.Always, null, "^.*?(?<!.zip)$", "", true);
            }
            catch (ZipException e)
            {
                throw e;
            }
        }
    }
}
