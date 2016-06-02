using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace KellPersistence
{
    public interface ICustomObject : ICloneable
    {
        Guid ID { get; set; }
        DataHeader Header { get; set; }
    }

    [Serializable]
    public class DataHeader
    {
        ITrunk trunk;

        public ITrunk Trunk
        {
            get { return trunk; }
        }

        int size;

        public int Size
        {
            get { return size; }
            set { size = value; }
        }
        string user;

        public string User
        {
            get { return user; }
            set { user = value; }
        }
        DateTime modifyTime;

        public DateTime ModifyTime
        {
            get { return modifyTime; }
            set { modifyTime = value; }
        }
        ulong identity;

        public ulong Identity
        {
            get { return identity; }
            set { identity = value; }
        }

        internal DataHeader(ITrunk trunk, int size, string user)
        {
            this.trunk = trunk;
            this.identity = trunk.Identity + 1;
            this.size = size;
            this.user = user;
            this.modifyTime = DateTime.Now;
        }

        public override string ToString()
        {
            return "{" + trunk.Name + "}[" + identity + "]Size=" + size + ",User=" + user + ",Time=" + modifyTime;
        }
    }

    [Serializable]
    public class Data : ICloneable
    {
        ITrunk trunk;

        public ITrunk Trunk
        {
            get { return trunk; }
        }
        Guid id;

        public Guid ID
        {
            get { return id; }
        }
        DataHeader header;

        public DataHeader Header
        {
            get { return header; }
        }
        byte[] buffer;

        public byte[] Buffer
        {
            get { return buffer; }
        }

        private Data(ITrunk trunk, Guid id, byte[] buffer, string user = "")
        {
            this.trunk = trunk;
            this.id = id;
            this.buffer = buffer;
            this.header = new DataHeader(trunk, buffer.Length, user);
        }

        public static Data Parse(ITrunk trunk, Guid id, byte[] buffer, string user = "")
        {
            return new Data(trunk, id, buffer, user);
        }

        public void Update(ulong identity, byte[] buffer, string user)
        {
            this.buffer = buffer;
            this.header.Identity = identity;
            this.header.Size = buffer.Length;
            this.header.User = user;
            this.header.ModifyTime = DateTime.Now;
        }

        public Data(ITrunk trunk, byte[] buffer, string user = "")
        {
            this.trunk = trunk;
            this.id = Guid.NewGuid();
            this.buffer = buffer;
            this.header = new DataHeader(trunk, buffer.Length, user);
        }

        public static Data Parse(ITrunk trunk, byte[] buffer, string user = "")
        {
            return new Data(trunk, buffer, user);
        }

        internal void SetIdentity(ulong identity)
        {
            this.header.Identity = identity;
        }

        public override string ToString()
        {
            return this.ID.ToString();
        }

        public object Clone()
        {
            byte[] cloneBuffer = new byte[this.buffer.Length];
            Array.Copy(this.buffer, cloneBuffer, this.buffer.Length);
            return new Data(this.trunk, this.id, cloneBuffer, this.Header.User);
        }
    }

    [Serializable]
    public class Data<T> : ICloneable
        where T : ICloneable
    {
        ITrunk trunk;

        public ITrunk Trunk
        {
            get { return trunk; }
        }
        Guid id;

        public Guid ID
        {
            get { return id; }
        }
        DataHeader header;

        public DataHeader Header
        {
            get { return header; }
        }
        T buffer;

        public T Buffer
        {
            get { return buffer; }
        }

        private Data(ITrunk trunk, Guid id, T buffer, string user = "")
        {
            this.trunk = trunk;
            this.id = id;
            this.buffer = buffer;
            int len = Common.GetBytes<T>(buffer).Length;
            this.header = new DataHeader(trunk, len, user);
        }

        public static Data<T> Parse(ITrunk trunk, Guid id, T buffer, string user = "")
        {
            return new Data<T>(trunk, id, buffer, user);
        }

        public void Update(ulong identity, string user, T buffer)
        {
            this.buffer = buffer;
            int len = Common.GetBytes<T>(buffer).Length;
            this.header.Identity = identity;
            this.header.Size = len;
            this.header.User = user;
            this.header.ModifyTime = DateTime.Now;
        }

        public Data(ITrunk trunk, T buffer, string user = "")
        {
            this.trunk = trunk;
            this.id = Guid.NewGuid();
            this.buffer = buffer;
            int len = Common.GetBytes<T>(buffer).Length;
            this.header = new DataHeader(trunk, len, user);
        }

        public static Data<T> Parse(ITrunk trunk, T buffer, string user = "")
        {
            return new Data<T>(trunk, buffer, user);
        }

        internal void SetIdentity(ulong identity)
        {
            this.header.Identity = identity;
        }

        public override string ToString()
        {
            return this.ID.ToString();
        }

        public object Clone()
        {
            return new Data<T>(this.trunk, this.id, (T)this.Buffer.Clone(), this.Header.User);
        }
    }

    [Serializable]
    public class TrunkInfo<T>
        where T : ICloneable
    {
        string name;

        public string Name
        {
            get { return name; }
            set { name = value; }
        }
        T data;

        public T Data
        {
            get { return data; }
            set { data = value; }
        }

        public TrunkInfo(string name, T data)
        {
            this.name = name;
            this.data = data;
        }

        public override string ToString()
        {
            return "[" + name + "]" + data.ToString();
        }
    }
}
