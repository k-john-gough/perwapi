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


namespace QUT.PERWAPI
{
    /**************************************************************************/
    // Descriptors for Native Types for parameter marshalling
    /**************************************************************************/
    /// <summary>
    /// Descriptors for native types used for marshalling
    /// </summary>
    public class NativeType
    {
        public static readonly NativeType Void = new NativeType(0x01);
        public static readonly NativeType Boolean = new NativeType(0x02);
        public static readonly NativeType Int8 = new NativeType(0x03);
        public static readonly NativeType UInt8 = new NativeType(0x04);
        public static readonly NativeType Int16 = new NativeType(0x05);
        public static readonly NativeType UInt16 = new NativeType(0x06);
        public static readonly NativeType Int32 = new NativeType(0x07);
        public static readonly NativeType UInt32 = new NativeType(0x08);
        public static readonly NativeType Int64 = new NativeType(0x09);
        public static readonly NativeType UInt64 = new NativeType(0x0A);
        public static readonly NativeType Float32 = new NativeType(0x0B);
        public static readonly NativeType Float64 = new NativeType(0x0C);
        public static readonly NativeType Currency = new NativeType(0x0F);
        public static readonly NativeType BStr = new NativeType(0x13);
        public static readonly NativeType LPStr = new NativeType(0x14);
        public static readonly NativeType LPWStr = new NativeType(0x15);
        public static readonly NativeType LPTStr = new NativeType(0x16);
        public static readonly NativeType FixedSysString = new NativeType(0x17);
        public static readonly NativeType IUnknown = new NativeType(0x19);
        public static readonly NativeType IDispatch = new NativeType(0x1A);
        public static readonly NativeType Struct = new NativeType(0x1B);
        public static readonly NativeType Interface = new NativeType(0x1C);
        public static readonly NativeType Int = new NativeType(0x1F);
        public static readonly NativeType UInt = new NativeType(0x20);
        public static readonly NativeType ByValStr = new NativeType(0x22);
        public static readonly NativeType AnsiBStr = new NativeType(0x23);
        public static readonly NativeType TBstr = new NativeType(0x24);
        public static readonly NativeType VariantBool = new NativeType(0x25);
        public static readonly NativeType FuncPtr = new NativeType(0x26);
        public static readonly NativeType AsAny = new NativeType(0x28);
        private static readonly NativeType[] nativeTypes = { null, Void, Boolean, Int8,  
                                                               UInt8, Int16, UInt16, Int32,
                                                               UInt32, Int64,  UInt64, 
                                                               Float32,  Float64, null, null,
                                                               Currency, null, null, null,
                                                               BStr, LPStr, LPWStr, LPTStr,
                                                               FixedSysString, null, IUnknown,
                                                               IDispatch, Struct, Interface,
                                                               null, null, Int, UInt, null,
                                                               ByValStr, AnsiBStr, TBstr,
                                                               VariantBool, FuncPtr, null,
                                                               AsAny};

        protected byte typeIndex;

        /*-------------------- Constructors ---------------------------------*/

        internal NativeType(byte tyIx) { typeIndex = tyIx; }

        internal byte GetTypeIndex() { return typeIndex; }

        internal static NativeType GetNativeType(int ix)
        {
            if (ix < nativeTypes.Length)
                return nativeTypes[ix];
            return null;
        }

        internal virtual byte[] ToBlob()
        {
            byte[] bytes = new byte[1];
            bytes[0] = GetTypeIndex();
            return bytes;
        }

        internal void Write(CILWriter output)
        {
            throw new NotYetImplementedException("Native types for CIL");
        }

    }

    /**************************************************************************/
    public class NativeArray : NativeType
    {
        NativeType elemType;
        uint len = 0, parNum = 0, elemMult = 1;
        internal static readonly byte ArrayTag = 0x2A;

        /*-------------------- Constructors ---------------------------------*/

        public NativeArray(NativeType elemType)
            : base((byte)NativeTypeIx.Array)
        {
            this.elemType = elemType;
        }

        public NativeArray(NativeType elemType, int len)
            : base((byte)NativeTypeIx.Array)
        {
            this.elemType = elemType;
            this.len = (uint)len;
        }

        public NativeArray(NativeType elemType, int numElem, int parNumForLen)
            : base((byte)NativeTypeIx.Array)
        {
            this.elemType = elemType;
            len = (uint)numElem;
            parNum = (uint)parNumForLen;
        }

        internal NativeArray(NativeType elemType, uint pNum, uint elemMult, uint numElem)
            : base((byte)NativeTypeIx.Array)
        {
            this.elemType = elemType;
            parNum = pNum;
            this.elemMult = elemMult;
            len = numElem;
        }

        internal override byte[] ToBlob()
        {
            MemoryStream str = new MemoryStream();
            str.WriteByte(GetTypeIndex());
            if (elemType == null) str.WriteByte(0x50);  // no info (MAX)
            else str.WriteByte(elemType.GetTypeIndex());
            MetaDataOut.CompressNum(BlobUtil.CompressUInt(parNum), str);
            MetaDataOut.CompressNum(BlobUtil.CompressUInt(elemMult), str);
            MetaDataOut.CompressNum(BlobUtil.CompressUInt(len), str);
            return str.ToArray();
        }

    }
}
