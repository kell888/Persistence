using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;
using System.Threading;

namespace KellPersistence
{
    public enum TrunkStatus : byte
    {
        /// <summary>
        /// 未同步数据到磁盘
        /// </summary>
        UnSync = 0,
        /// <summary>
        /// 正在跟磁盘同步数据
        /// </summary>
        Processing = 1,
        /// <summary>
        /// 已经同步磁盘数据
        /// </summary>
        Ready = 2
    }

    [Serializable]
    public class Trunk : ITrunk
    {
        string name = "Table1";
        ulong identity;
        static readonly object synObj = new object();
        SortedDictionary<Guid, Data> datas;
        volatile TrunkStatus status;
        bool isDrop;
        bool diskOnly;

        public static List<string> Tables
        {
            get
            {
                List<string> tables = new List<string>();
                string root = Common.CurrentPath + "\\Trunks";
                if (Directory.Exists(root))
                {
                    string[] dirs = Directory.GetDirectories(root);
                    foreach (string dir in dirs)
                    {
                        string table = Path.GetFileName(dir);
                        tables.Add(table);
                    }
                }
                return tables;
            }
        }

        public bool DiskOnly
        {
            get { return diskOnly; }
        }

        public ulong Identity
        {
            get { return identity; }
        }

        public int DataCount
        {
            get
            {
                lock (synObj)
                {
                    int persCount = this.PersCount;
                    if (status == TrunkStatus.UnSync && datas != null)
                    {
                        return datas.Count + persCount;
                    }
                    return persCount;
                }
            }
        }

        public bool IsDrop
        {
            get { return isDrop; }
        }

        private int PersCount
        {
            get
            {
                lock (synObj)
                {
                    try
                    {
                        string[] files = Directory.GetFiles(TrunkPath, "*.KPF");
                        return files.Length;
                    }
                    catch
                    {
                        return 0;
                    }
                }
            }
        }

        public string TrunkPath
        {
            get
            {
                string path = Common.CurrentPath + "\\Trunks\\" + name;
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }
        }

        public string Name
        {
            get { return name; }
        }

        public bool Rename(string newName)
        {
            string path = Common.CurrentPath + "\\Trunks\\" + name;
            string newPath = Common.CurrentPath + "\\Trunks\\" + newName;
            if (Directory.Exists(newPath))
            {
                throw new ArgumentException("系统中已经存在该表名，请换另一个名字！", "newName");
            }
            try
            {
                File.Move(path, newPath);
                this.name = newName;
                this.isDrop = false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public TrunkStatus Status
        {
            get { return status; }
        }
        /// <summary>
        /// 新建表
        /// </summary>
        /// <param name="diskOnly"></param>
        public Trunk(bool diskOnly = false)
        {
            if (!Directory.Exists(Common.CurrentPath + "\\Trunks"))
                Directory.CreateDirectory(Common.CurrentPath + "\\Trunks");

            string path = Common.CurrentPath + "\\Trunks\\" + name;
            while (Directory.Exists(path))
            {
                name += "0";
                path = Common.CurrentPath + "\\Trunks\\" + name;
            }
            if (path.Length > Common.MaxDirLength)
                throw new Exception("表的绝对路径太长，path.Length=" + path.Length + ">" + Common.MaxDirLength);

            this.diskOnly = diskOnly;
            datas = new SortedDictionary<Guid, Data>();
            identity = GetCurrentIdentity();
        }
        /// <summary>
        /// 打开或者新建表
        /// </summary>
        /// <param name="name"></param>
        /// <param name="diskOnly"></param>
        public Trunk(string name, bool diskOnly = false)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException("name");

            if (!Common.IsValidFileName(name))
                throw new ArgumentException("非法表名", "name");

            string path = Common.CurrentPath + "\\Trunks\\" + name;
            if (path.Length > Common.MaxDirLength)
                throw new Exception("表的绝对路径太长，path.Length=" + path.Length + ">" + Common.MaxDirLength);

            this.diskOnly = diskOnly;
            this.name = name;
            datas = new SortedDictionary<Guid, Data>();
            identity = GetCurrentIdentity();
        }

        private ulong GetCurrentIdentity()
        {
            lock (synObj)
            {
                ulong iden = (ulong)PersCount;
                using (FileStream fs = new FileStream(TrunkPath + "\\identity", FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        if (sr.EndOfStream)
                        {
                            using (StreamWriter sw = new StreamWriter(fs))
                            {
                                sw.WriteLine(PersCount);
                            }
                        }
                        else
                        {
                            string s = sr.ReadLine();
                            iden = ulong.Parse(s);
                        }
                    }
                }
                return iden;
            }
        }

        private void SynIdentity()
        {
            lock (synObj)
            {
                using (FileStream fs = new FileStream(TrunkPath + "\\identity", FileMode.Create, FileAccess.ReadWrite))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        sw.WriteLine(identity);
                    }
                }
            }
        }

        public List<Data> SelectDisk(ulong start, ulong end)
        {
            List<Data> all = new List<Data>();
            if (start > 0 && end >= start)
            {
                SortedDictionary<ulong, Data> tmp = new SortedDictionary<ulong, Data>();
                string[] files = Directory.GetFiles(TrunkPath, "*.KPF");
                foreach (string file in files)
                {
                    Guid id = new Guid(Path.GetFileNameWithoutExtension(file));
                    Data data = GetDataFromFile(id);
                    tmp.Add(data.Header.Identity, data);
                }
                IEnumerable<KeyValuePair<ulong, Data>> ie = tmp.TakeWhile<KeyValuePair<ulong, Data>>(a => a.Key >= start && a.Key <= end);
                using (IEnumerator<KeyValuePair<ulong, Data>> iee = ie.GetEnumerator())
                {
                    while (iee.MoveNext())
                    {
                        all.Add(iee.Current.Value);
                    }
                }
            }
            return all;
        }

        public Data Select(Guid id)
        {
            if (!diskOnly)
            {
                if (datas.ContainsKey(id))
                    return datas[id];
                else
                    return null;
            }
            else
            {
                return GetDataFromFile(id);
            }
        }

        public List<Data> Select(string user)
        {
            List<Data> ds = new List<Data>();
            if (user == null)
                return ds;
            SortedDictionary<ulong, Data> tmp = new SortedDictionary<ulong, Data>();
            if (!diskOnly)
            {
                if (datas.Count > 0)
                {
                    foreach (Guid key in datas.Keys)
                    {
                        Data data = datas[key];
                        if (user.Equals(data.Header.User, StringComparison.InvariantCultureIgnoreCase))
                        {
                            tmp.Add(data.Header.Identity, data);
                        }
                    }
                }
            }
            else
            {
                string[] files = Directory.GetFiles(TrunkPath, "*.KPF");
                foreach (string file in files)
                {
                    Guid id = new Guid(Path.GetFileNameWithoutExtension(file));
                    Data data = GetDataFromFile(id);
                    if (user == data.Header.User)
                    {
                        tmp.Add(data.Header.Identity, data);
                    }
                }
            }
            foreach (ulong key in tmp.Keys)
            {
                ds.Add(tmp[key]);
            }
            return ds;
        }

        public List<Data> SelectAll()
        {
            List<Data> all = new List<Data>();
            SortedDictionary<ulong, Data> tmp = new SortedDictionary<ulong, Data>();
            if (!diskOnly)
            {
                if (datas.Count > 0)
                {
                    foreach (Guid key in datas.Keys)
                    {
                        Data data = datas[key];
                        tmp.Add(data.Header.Identity, data);
                    }
                }
            }
            else
            {
                string[] files = Directory.GetFiles(TrunkPath, "*.KPF");
                foreach (string file in files)
                {
                    Guid id = new Guid(Path.GetFileNameWithoutExtension(file));
                    Data data = GetDataFromFile(id);
                    tmp.Add(data.Header.Identity, data);
                }
            }
            foreach (ulong key in tmp.Keys)
            {
                all.Add(tmp[key]);
            }
            return all;
        }

        public bool Insert(Data data, bool diskOnly = false)
        {
            lock (synObj)
            {
                try
                {
                    identity++;
                    data.SetIdentity(identity);
                    if (!diskOnly)
                        datas.Add(data.ID, data);
                    status = TrunkStatus.UnSync;
                    FlushToFile(data);
                    return true;
                }
                catch { return false; }
            }
        }

        public bool Update(Data data)
        {
            lock (synObj)
            {
                try
                {
                    datas[data.ID] = data;
                    status = TrunkStatus.UnSync;
                    FlushToFile(data);
                    return true;
                }
                catch { return false; }
            }
        }

        public bool Delete(Guid id)
        {
            lock (synObj)
            {
                try
                {
                    datas.Remove(id);
                    status = TrunkStatus.UnSync;
                    DeleteFile(id);
                    return true;
                }
                catch { return false; }
            }
        }

        public bool Truncate()
        {
            lock (synObj)
            {
                try
                {
                    datas.Clear();
                    status = TrunkStatus.UnSync;
                    ClearFiles();
                    return true;
                }
                catch { return false; }
            }
        }

        public bool Drop()
        {
            lock (synObj)
            {
                try
                {
                    datas.Clear();
                    status = TrunkStatus.UnSync;
                    DeleteDir();
                    isDrop = true;
                    return true;
                }
                catch { return false; }
            }
        }

        public bool Bulk(string outDir, bool input = false)
        {
            try
            {
                if (input)
                {
                    string dirName = Path.GetFileName(outDir);
                    this.name = dirName;
                    this.isDrop = false;
                    ImportFromDir(outDir);
                }
                else
                {
                    ExportToDir(outDir);
                }
                return true;
            }
            catch { return false; }
        }

        public static bool ZipTable(string sourceDir, string zipFilepath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(zipFilepath))
                {
                    string dirName = Path.GetFileName(sourceDir);
                    zipFilepath = sourceDir + "\\" + dirName + ".zip";
                }
                Common.Zip(zipFilepath, sourceDir);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool UnzipTable(string zipFilepath, string destDir = null)
        {
            try
            {
                if (string.IsNullOrEmpty(destDir))
                {
                    destDir = Path.GetDirectoryName(zipFilepath);
                }
                Common.Unzip(zipFilepath, destDir);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ExportToDir(string destDir)
        {
            string[] files = Directory.GetFiles(TrunkPath);
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                File.Copy(file, destDir + "\\" + filename, true);
            }
        }

        private void ImportFromDir(string destDir)
        {
            datas.Clear();
            status = TrunkStatus.UnSync;
            string[] files = Directory.GetFiles(destDir);
            foreach (string file in files)
            {
                status = TrunkStatus.Processing;
                string filename = Path.GetFileName(file);
                File.Copy(file, TrunkPath + "\\" + filename, true);
                List<byte> append = new List<byte>();
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.ReadWrite))
                {
                    byte[] buffer = new byte[1024];
                    int len = 0;
                    while ((len = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        byte[] real = new byte[len];
                        Array.Copy(buffer, real, len);
                        append.AddRange(real);
                    }
                }
                Data data = Common.GetObject<Data>(append.ToArray());
                datas.Add(data.ID, data);
            }
            identity = GetCurrentIdentity();
            status = TrunkStatus.Ready;
        }

        private void DeleteFile(Guid id)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(DoDeleteData), id);
        }

        private void DoDeleteData(object o)
        {
            Guid id = (Guid)o;
            status = TrunkStatus.Processing;
            File.Delete(TrunkPath + "\\" + id + ".KPF");
            status = TrunkStatus.Ready;
        }

        private void ClearFiles()
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(DoClear));
        }

        private void DoClear(object o)
        {
            status = TrunkStatus.Processing;
            string[] files = Directory.GetFiles(TrunkPath, "*.KPF");
            foreach (string file in files)
            {
                File.Delete(file);
            }
            status = TrunkStatus.Ready;
        }

        private void DeleteDir()
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(DoDeleteTable));
        }

        private void DoDeleteTable(object o)
        {
            status = TrunkStatus.Processing;
            Directory.Delete(TrunkPath, true);
            status = TrunkStatus.Ready;
        }

        private void FlushToFile(Data data)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(DoFlush), data);
        }

        private void DoFlush(object o)
        {
            Data data = o as Data;
            status = TrunkStatus.Processing;
            byte[] append = Common.GetBytes(data);
            using (FileStream fs = new FileStream(TrunkPath + "\\" + data.ID + ".KPF", FileMode.Create, FileAccess.ReadWrite))
            {
                fs.Write(append, 0, append.Length);
                fs.Flush();
            }
            SynIdentity();
            status = TrunkStatus.Ready;
        }

        private Data GetDataFromFile(Guid id)
        {
            List<byte> append = new List<byte>();
            status = TrunkStatus.Processing;
            using (FileStream fs = new FileStream(TrunkPath + "\\" + id + ".KPF", FileMode.Open, FileAccess.ReadWrite))
            {
                byte[] buffer = new byte[1024];
                int len = 0;
                while ((len = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    byte[] real = new byte[len];
                    Array.Copy(buffer, real, len);
                    append.AddRange(real);
                }
            }
            status = TrunkStatus.Ready;
            return Common.GetData(append.ToArray());
        }
    }

    [Serializable]
    public class Trunk<T> : ITrunk
        where T : ICloneable
    {
        string name = "Table1";
        ulong identity;
        static readonly object synObj = new object();
        SortedDictionary<Guid, Data<T>> datas;
        volatile TrunkStatus status;
        bool isDrop;
        bool diskOnly;

        public static List<string> Tables
        {
            get
            {
                List<string> tables = new List<string>();
                string root = Common.CurrentPath + "\\Trunks";
                if (Directory.Exists(root))
                {
                    string[] dirs = Directory.GetDirectories(root);
                    foreach (string dir in dirs)
                    {
                        string table = Path.GetFileName(dir);
                        tables.Add(table);
                    }
                }
                return tables;
            }
        }

        public bool DiskOnly
        {
            get { return diskOnly; }
        }

        public ulong Identity
        {
            get { return identity; }
        }

        public int DataCount
        {
            get
            {
                lock (synObj)
                {
                    int persCount = this.PersCount;
                    if (status == TrunkStatus.UnSync && datas != null)
                    {
                        return datas.Count + persCount;
                    }
                    return persCount;
                }
            }
        }

        public bool IsDrop
        {
            get { return isDrop; }
        }

        public int PersCount
        {
            get
            {
                lock (synObj)
                {
                    try
                    {
                        string[] files = Directory.GetFiles(TrunkPath, "*.KPF");
                        return files.Length;
                    }
                    catch
                    {
                        return 0;
                    }
                }
            }
        }

        public string TrunkPath
        {
            get
            {
                string path = Common.CurrentPath + "\\Trunks\\" + name;
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }
        }

        public string Name
        {
            get { return name; }
        }

        public bool Rename(string newName)
        {
            string path = Common.CurrentPath + "\\Trunks\\" + name;
            string newPath = Common.CurrentPath + "\\Trunks\\" + newName;
            if (Directory.Exists(newPath))
            {
                throw new ArgumentException("系统中已经存在该表名，请换另一个名字！", "newName");
            }
            try
            {
                File.Move(path, newPath);
                this.name = newName;
                this.isDrop = false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public TrunkStatus Status
        {
            get { return status; }
        }
        /// <summary>
        /// 新建表
        /// </summary>
        /// <param name="diskOnly"></param>
        public Trunk(bool diskOnly = false)
        {
            if (!Directory.Exists(Common.CurrentPath + "\\Trunks"))
                Directory.CreateDirectory(Common.CurrentPath + "\\Trunks");

            string path = Common.CurrentPath + "\\Trunks\\" + name;
            while (Directory.Exists(path))
            {
                name += "0";
                path = Common.CurrentPath + "\\Trunks\\" + name;
            }
            if (path.Length > Common.MaxDirLength)
                throw new Exception("表的绝对路径太长，path.Length=" + path.Length + ">" + Common.MaxDirLength);

            this.diskOnly = diskOnly;
            datas = new SortedDictionary<Guid, Data<T>>();
            identity = GetCurrentIdentity();
        }
        /// <summary>
        /// 打开或者新建表
        /// </summary>
        /// <param name="name"></param>
        /// <param name="diskOnly"></param>
        public Trunk(string name, bool diskOnly = false)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException("name");

            if (!Common.IsValidFileName(name))
                throw new ArgumentException("非法表名", "name");

            string path = Common.CurrentPath + "\\Trunks\\" + name;
            if (path.Length > Common.MaxDirLength)
                throw new Exception("表的绝对路径太长，path.Length=" + path.Length + ">" + Common.MaxDirLength);

            this.diskOnly = diskOnly;
            this.name = name;
            datas = new SortedDictionary<Guid, Data<T>>();
            identity = GetCurrentIdentity();
        }

        private ulong GetCurrentIdentity()
        {
            lock (synObj)
            {
                ulong iden = (ulong)PersCount;
                using (FileStream fs = new FileStream(TrunkPath + "\\identity", FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        if (sr.EndOfStream)
                        {
                            using (StreamWriter sw = new StreamWriter(fs))
                            {
                                sw.WriteLine(PersCount);
                            }
                        }
                        else
                        {
                            string s = sr.ReadLine();
                            iden = ulong.Parse(s);
                        }
                    }
                }
                return iden;
            }
        }

        private void SynIdentity()
        {
            lock (synObj)
            {
                using (FileStream fs = new FileStream(TrunkPath + "\\identity", FileMode.Create, FileAccess.ReadWrite))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        sw.WriteLine(identity);
                    }
                }
            }
        }

        public List<Data<T>> SelectDisk(ulong start, ulong end)
        {
            List<Data<T>> all = new List<Data<T>>();
            if (start > 0 && end >= start)
            {
                SortedDictionary<ulong, Data<T>> tmp = new SortedDictionary<ulong, Data<T>>();
                string[] files = Directory.GetFiles(TrunkPath, "*.KPF");
                foreach (string file in files)
                {
                    Guid id = new Guid(Path.GetFileNameWithoutExtension(file));
                    Data<T> data = GetDataFromFile(id);
                    if (data != null)
                    {
                        tmp.Add(data.Header.Identity, data);
                    }
                }
                IEnumerable<KeyValuePair<ulong, Data<T>>> ie = tmp.TakeWhile<KeyValuePair<ulong, Data<T>>>(a => a.Key >= start && a.Key <= end);
                using (IEnumerator<KeyValuePair<ulong, Data<T>>> iee = ie.GetEnumerator())
                {
                    while (iee.MoveNext())
                    {
                        all.Add(iee.Current.Value);
                    }
                }
            }
            return all;
        }

        public Data<T> Select(Guid id)
        {
            if (!diskOnly)
            {
                if (datas.ContainsKey(id))
                    return datas[id];
                else
                    return null;
            }
            else
            {
                return GetDataFromFile(id);
            }
        }

        public List<Data<T>> Select(string user)
        {
            List<Data<T>> ds = new List<Data<T>>();
            if (user == null)
                return ds;
            SortedDictionary<ulong, Data<T>> tmp = new SortedDictionary<ulong, Data<T>>();
            if (!diskOnly)
            {
                if (datas.Count > 0)
                {
                    foreach (Guid key in datas.Keys)
                    {
                        Data<T> data = datas[key];
                        if (user.Equals(data.Header.User, StringComparison.InvariantCultureIgnoreCase))
                        {
                            tmp.Add(data.Header.Identity, data);
                        }
                    }
                }
            }
            else
            {
                string[] files = Directory.GetFiles(TrunkPath, "*.KPF");
                foreach (string file in files)
                {
                    Guid id = new Guid(Path.GetFileNameWithoutExtension(file));
                    Data<T> data = GetDataFromFile(id);
                    if (data != null)
                    {
                        if (user == data.Header.User)
                        {
                            tmp.Add(data.Header.Identity, data);
                        }
                    }
                }
            }
            foreach (ulong key in tmp.Keys)
            {
                ds.Add(tmp[key]);
            }
            return ds;
        }

        public List<Data<T>> SelectAll()
        {
            List<Data<T>> all = new List<Data<T>>();
            SortedDictionary<ulong, Data<T>> tmp = new SortedDictionary<ulong, Data<T>>();
            if (!diskOnly)
            {
                if (datas.Count > 0)
                {
                    foreach (Guid key in datas.Keys)
                    {
                        Data<T> data = datas[key];
                        tmp.Add(data.Header.Identity, data);
                    }
                }
            }
            else
            {
                string[] files = Directory.GetFiles(TrunkPath, "*.KPF");
                foreach (string file in files)
                {
                    Guid id = new Guid(Path.GetFileNameWithoutExtension(file));
                    Data<T> data = GetDataFromFile(id);
                    if (data != null)
                    {
                        tmp.Add(data.Header.Identity, data);
                    }
                }
            }
            foreach (ulong key in tmp.Keys)
            {
                all.Add(tmp[key]);
            }
            return all;
        }

        public bool Insert(Data<T> data)
        {
            lock (synObj)
            {
                try
                {
                    identity++;
                    data.SetIdentity(identity);
                    if (!diskOnly)
                        datas.Add(data.ID, data);
                    status = TrunkStatus.UnSync;
                    FlushToFile(data);
                    return true;
                }
                catch { return false; }
            }
        }

        public bool Update(Data<T> data)
        {
            lock (synObj)
            {
                try
                {
                    datas[data.ID] = data;
                    status = TrunkStatus.UnSync;
                    FlushToFile(data);
                    return true;
                }
                catch { return false; }
            }
        }

        public bool Delete(Guid id)
        {
            lock (synObj)
            {
                try
                {
                    datas.Remove(id);
                    status = TrunkStatus.UnSync;
                    DeleteFile(id);
                    return true;
                }
                catch { return false; }
            }
        }

        public bool Truncate()
        {
            lock (synObj)
            {
                try
                {
                    datas.Clear();
                    status = TrunkStatus.UnSync;
                    ClearFiles();
                    return true;
                }
                catch { return false; }
            }
        }

        public bool Drop()
        {
            lock (synObj)
            {
                try
                {
                    datas.Clear();
                    status = TrunkStatus.UnSync;
                    DeleteDir();
                    isDrop = true;
                    return true;
                }
                catch { return false; }
            }
        }

        public bool Bulk(string outDir, bool input = false)
        {
            try
            {
                if (input)
                {
                    string dirName = Path.GetFileName(outDir);
                    this.name = dirName;
                    this.isDrop = false;
                    ImportFromDir(outDir);
                }
                else
                {
                    ExportToDir(outDir);
                }
                return true;
            }
            catch { return false; }
        }

        public static bool ZipTable(string sourceDir, string zipFilepath = null, bool containsChildren = false)
        {
            try
            {
                if (string.IsNullOrEmpty(zipFilepath))
                {
                    string dirName = Path.GetFileName(sourceDir);
                    zipFilepath = sourceDir + "\\" + dirName + ".zip";
                }
                Common.Zip(zipFilepath, sourceDir, containsChildren);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool UnzipTable(string zipFilepath, string destDir = null)
        {
            try
            {
                if (string.IsNullOrEmpty(destDir))
                {
                    destDir = Path.GetDirectoryName(zipFilepath);
                }
                Common.Unzip(zipFilepath, destDir);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ExportToDir(string destDir, bool containsChildren = false)
        {
            string[] files = Directory.GetFiles(TrunkPath, "*.*", SearchOption.TopDirectoryOnly);
            if (containsChildren)
                files = Directory.GetFiles(TrunkPath, "*.*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                File.Copy(file, destDir + "\\" + filename, true);
            }
        }

        private void ImportFromDir(string destDir, bool containsChildren = false)
        {
            datas.Clear();
            status = TrunkStatus.UnSync;
            string[] files = Directory.GetFiles(destDir, "*.*", SearchOption.TopDirectoryOnly);
            if (containsChildren)
                files = Directory.GetFiles(TrunkPath, "*.*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                status = TrunkStatus.Processing;
                string filename = Path.GetFileName(file);
                string ext = Path.GetExtension(file);
                File.Copy(file, TrunkPath + "\\" + filename, true);
                if (ext.Equals(".KPF", StringComparison.InvariantCultureIgnoreCase))
                {
                    List<byte> append = new List<byte>();
                    using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.ReadWrite))
                    {
                        byte[] buffer = new byte[1024];
                        int len = 0;
                        while ((len = fs.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            byte[] real = new byte[len];
                            Array.Copy(buffer, real, len);
                            append.AddRange(real);
                        }
                    }
                    Data<T> data = Common.GetObject<Data<T>>(append.ToArray());
                    if (data != null)
                        datas.Add(data.ID, data);
                }
            }
            identity = GetCurrentIdentity();
            status = TrunkStatus.Ready;
        }

        private void DeleteFile(Guid id)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(DoDeleteData), id);
        }

        private void DoDeleteData(object o)
        {
            Guid id = (Guid)o;
            status = TrunkStatus.Processing;
            File.Delete(TrunkPath + "\\" + id + ".KPF");
            status = TrunkStatus.Ready;
        }

        private void ClearFiles()
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(DoClear));
        }

        private void DoClear(object o)
        {
            status = TrunkStatus.Processing;
            string[] files = Directory.GetFiles(TrunkPath, "*.KPF");
            foreach (string file in files)
            {
                File.Delete(file);
            }
            status = TrunkStatus.Ready;
        }

        private void DeleteDir()
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(DoDeleteTable));
        }

        private void DoDeleteTable(object o)
        {
            status = TrunkStatus.Processing;
            Directory.Delete(TrunkPath, true);
            status = TrunkStatus.Ready;
        }

        private void FlushToFile(Data<T> data)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(DoFlush), data);
        }

        private void DoFlush(object o)
        {
            Data<T> data = o as Data<T>;
            status = TrunkStatus.Processing;
            byte[] append = Common.GetBytes<Data<T>>(data);
            using (FileStream fs = new FileStream(TrunkPath + "\\" + data.ID + ".KPF", FileMode.Create, FileAccess.ReadWrite))
            {
                fs.Write(append, 0, append.Length);
                fs.Flush();
            }
            SynIdentity();
            status = TrunkStatus.Ready;
        }

        private Data<T> GetDataFromFile(Guid id)
        {
            List<byte> append = new List<byte>();
            status = TrunkStatus.Processing;
            using (FileStream fs = new FileStream(TrunkPath + "\\" + id + ".KPF", FileMode.Open, FileAccess.ReadWrite))
            {
                byte[] buffer = new byte[1024];
                int len = 0;
                while ((len = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    byte[] real = new byte[len];
                    Array.Copy(buffer, real, len);
                    append.AddRange(real);
                }
            }
            status = TrunkStatus.Ready;
            return Common.GetObject<Data<T>>(append.ToArray());
        }
    }
}
