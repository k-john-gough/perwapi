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
    /*****************************************************************************/
    /// <summary>
    /// Marshalling information for a field or param
    /// </summary>
    public class FieldMarshal : MetaDataElement
    {
        MetaDataElement field;
        NativeType nt;
        uint ntIx, parentIx;

        /*-------------------- Added by Carlo Kok ---------------------------------*/

        private SafeArrayType safeArraySubType;
        public SafeArrayType SafeArraySubType { get { return safeArraySubType; ; } set { safeArraySubType = value; } }

        private string safeArrayUserDefinedSubType;
        public string SafeArrayUserDefinedSubType { get { return safeArrayUserDefinedSubType; } set { safeArrayUserDefinedSubType = value; } }

        private NativeTypeIx arraySubType = (NativeTypeIx)0x50; // default, important
        public NativeTypeIx ArraySubType { get { return arraySubType; } set { arraySubType = value; } }

        private int sizeConst = -1;
        public int SizeConst { get { return sizeConst; } set { sizeConst = value; } }

        private int sizeParamIndex = -1;
        public int SizeParamIndex { get { return sizeParamIndex; } set { sizeParamIndex = value; } }

        private string customMarshallingType;
        public string CustomMarshallingType { get { return customMarshallingType; } set { customMarshallingType = value; } }

        private string customMarshallingCookie;
        public string CustomMarshallingCookie { get { return customMarshallingCookie; } set { customMarshallingCookie = value; } }

        /*-------------------- Constructors ---------------------------------*/

        internal FieldMarshal(MetaDataElement field, NativeType nType)
        {
            this.field = field;
            this.nt = nType;
            sortTable = true;
            tabIx = MDTable.FieldMarshal;
        }

        internal FieldMarshal(PEReader buff)
        {
            parentIx = buff.GetCodedIndex(CIx.HasFieldMarshal);
            ntIx = buff.GetBlobIx();
            sortTable = true;
            tabIx = MDTable.FieldMarshal;
        }

        internal static void Read(PEReader buff, TableRow[] fMarshal)
        {
            for (int i = 0; i < fMarshal.Length; i++)
                fMarshal[i] = new FieldMarshal(buff);
        }

        internal override void Resolve(PEReader buff)
        {
            field = buff.GetCodedElement(CIx.HasFieldMarshal, parentIx);
            nt = buff.GetBlobNativeType(ntIx);
            if (field is FieldDef)
            {
                ((FieldDef)field).SetMarshalType(nt);
            }
            else
            {
                ((Param)field).SetMarshalType(nt);
            }
        }

        internal override uint SortKey()
        {
            return (field.Row << MetaData.CIxShiftMap[(uint)CIx.HasFieldMarshal])
                | field.GetCodedIx(CIx.HasFieldMarshal);
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.FieldMarshal, this);
            ntIx = md.AddToBlobHeap(nt.ToBlob());
        }

        internal static uint Size(MetaData md)
        {
            return md.CodedIndexSize(CIx.HasFieldMarshal) + md.BlobIndexSize();
        }

        internal sealed override void Write(PEWriter output)
        {
            output.WriteCodedIndex(CIx.HasFieldMarshal, field);
            output.BlobIndex(ntIx);
        }

    }


}