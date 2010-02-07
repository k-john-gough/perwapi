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
    /// <summary>
    /// Descriptor for a class defined in another module of THIS assembly 
    /// and exported (.class extern)
    /// </summary>
    internal class ExternClass : MetaDataElement
    {
        MetaDataElement implementation;
        uint flags, typeDefId = 0;
        uint implIx = 0, nameIx = 0, nameSpaceIx = 0;
        string nameSpace, name;

        /*-------------------- Constructors ---------------------------------*/

        internal ExternClass(TypeAttr attr, string ns, string name, MetaDataElement paren)
        {
            flags = (uint)attr;
            nameSpace = ns;
            this.name = name;
            implementation = paren;
            tabIx = MDTable.ExportedType;
        }

        public ExternClass(PEReader buff)
        {
            flags = buff.ReadUInt32();
            typeDefId = buff.ReadUInt32();
            name = buff.GetString();
            nameSpace = buff.GetString();
            implIx = buff.GetCodedIndex(CIx.Implementation);
            tabIx = MDTable.ExportedType;
        }

        internal static void Read(PEReader buff, TableRow[] eClasses)
        {
            for (int i = 0; i < eClasses.Length; i++)
                eClasses[i] = new ExternClass(buff);
        }

        internal static void GetClassRefs(PEReader buff, TableRow[] eClasses)
        {
            for (uint i = 0; i < eClasses.Length; i++)
            {
                uint junk = buff.ReadUInt32();
                junk = buff.ReadUInt32();
                string name = buff.GetString();
                string nameSpace = buff.GetString();
                uint implIx = buff.GetCodedIndex(CIx.Implementation);
                eClasses[i] = new ClassRef(implIx, nameSpace, name);
                eClasses[i].Row = i + 1;
            }
        }

        internal override void Resolve(PEReader buff)
        {
            implementation = buff.GetCodedElement(CIx.Implementation, implIx);
            while (implementation is ExternClass)
                implementation = ((ExternClass)implementation).implementation;
            ((ModuleFile)implementation).fileModule.AddExternClass(this);
        }

        internal string NameSpace() { return nameSpace; }
        internal string Name() { return name; }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.ExportedType, this);
            nameSpaceIx = md.AddToStringsHeap(nameSpace);
            nameIx = md.AddToStringsHeap(name);
            if (implementation is ModuleRef)
            {
                ModuleFile mFile = ((ModuleRef)implementation).modFile;
                mFile.BuildMDTables(md);
                implementation = mFile;
            }
        }

        internal static uint Size(MetaData md)
        {
            return 8 + 2 * md.StringsIndexSize() + md.CodedIndexSize(CIx.Implementation);
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write(flags);
            output.Write(0);
            output.StringsIndex(nameIx);
            output.StringsIndex(nameSpaceIx);
            output.WriteCodedIndex(CIx.Implementation, implementation);
        }

        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.HasCustomAttr): return 17;
                case (CIx.Implementation): return 2;
            }
            return 0;
        }

    }


}