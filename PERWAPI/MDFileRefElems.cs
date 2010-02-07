/*  
 * PERWAPI - An API for Reading and Writing PE Files
 * 
 * Copyright (c) Diane Corney, Queensland University of Technology, 2004.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the PERWAPI Copyright as included with this
 * distribution in the file PERWAPIcopyright.rtf.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY as is explained in the copyright notice.
 *
 * The author may be contacted at d.corney@qut.edu.au
 * 
 * Version Date:  26/01/07
 */

using System;
using System.IO;
using System.Collections;
using System.Security.Cryptography;


namespace QUT.PERWAPI
{
    /**************************************************************************/
    public abstract class FileRef : MetaDataElement
    {
        protected static readonly uint HasMetaData = 0x0;
        protected static readonly uint HasNoMetaData = 0x1;
        protected uint nameIx = 0, hashIx = 0;
        protected byte[] hashBytes;
        protected string name;
        protected bool entryPoint = false;
        protected uint flags;

        /*-------------------- Constructors ---------------------------------*/

        internal FileRef(string name, byte[] hashBytes)
        {
            this.hashBytes = hashBytes;
            this.name = name;
            tabIx = MDTable.File;
        }

        internal FileRef(PEReader buff)
        {
            flags = buff.ReadUInt32();
            name = buff.GetString();
            hashBytes = buff.GetBlob();
            tabIx = MDTable.File;
        }

        internal static void Read(PEReader buff, TableRow[] files)
        {
            for (int i = 0; i < files.Length; i++)
            {
                uint flags = buff.ReadUInt32();
                if (flags == HasMetaData)
                    files[i] = new ModuleFile(buff.GetString(), buff.GetBlob());
                else
                    files[i] = new ResourceFile(buff.GetString(), buff.GetBlob());
            }
        }

        public string Name() { return name; }
        public byte[] GetHash() { return hashBytes; }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.File, this);
            nameIx = md.AddToStringsHeap(name);
            hashIx = md.AddToBlobHeap(hashBytes);
            if (entryPoint) md.SetEntryPoint(this);
        }

        internal static uint Size(MetaData md)
        {
            return 4 + md.StringsIndexSize() + md.BlobIndexSize();
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write(flags);
            output.StringsIndex(nameIx);
            output.BlobIndex(hashIx);
        }

        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.HasCustomAttr): return 16;
                case (CIx.Implementation): return 0;
            }
            return 0;
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a file referenced in THIS assembly/module (.file)
    /// </summary>
    internal class ModuleFile : FileRef
    {
        internal ModuleRef fileModule;

        internal ModuleFile(string name, byte[] hashBytes, bool entryPoint)
            : base(name, hashBytes)
        {
            flags = HasMetaData;
            this.entryPoint = entryPoint;
        }

        internal ModuleFile(string name, byte[] hashBytes)
            : base(name, hashBytes)
        {
            flags = HasMetaData;
        }

        internal void SetEntryPoint() { entryPoint = true; }

        internal void SetHash(byte[] hashVal) { hashBytes = hashVal; }


    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a file containing a managed resource
    /// </summary>
    public class ResourceFile : FileRef
    {
        static ArrayList files = new ArrayList();

        /*-------------------- Constructors ---------------------------------*/

        public ResourceFile(string name, byte[] hashValue)
            : base(name, hashValue)
        {
            flags = HasNoMetaData;
            files.Add(this);
        }

        public static ResourceFile GetFile(string name)
        {
            for (int i = 0; i < files.Count; i++)
            {
                if (((ResourceFile)files[i]).name.Equals(name))
                    return (ResourceFile)files[i];
            }
            return null;
        }

    }
}