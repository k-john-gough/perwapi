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
    /// Descriptor for a Custom Attribute (.custom) 
    /// </summary>
    public class CustomAttribute : MetaDataElement
    {
        internal static readonly ushort prolog = 0x0001;
        private static readonly int initSize = 5;
        MetaDataElement parent;
        Method type;
        uint valIx, parentIx, typeIx;
        Constant[] argVals, vals;
        byte[] byteVal;
        ushort numNamed = 0;
        string[] names;
        bool[] isField;
        bool changed = false;

        /*-------------------- Constructors ---------------------------------*/

        internal CustomAttribute(MetaDataElement paren, Method constrType,
            Constant[] val)
        {
            parent = paren;
            type = constrType;
            argVals = val;
            changed = true;
            sortTable = true;
            tabIx = MDTable.CustomAttribute;
        }

        internal CustomAttribute(MetaDataElement paren, Method constrType,
            byte[] val)
        {
            parent = paren;
            type = constrType;
            tabIx = MDTable.CustomAttribute;
            byteVal = val;
            sortTable = true;
            changed = true;
        }

        internal CustomAttribute(PEReader buff)
        {
            parentIx = buff.GetCodedIndex(CIx.HasCustomAttr);
            typeIx = buff.GetCodedIndex(CIx.CustomAttributeType);
            valIx = buff.GetBlobIx();
            sortTable = true;
            tabIx = MDTable.CustomAttribute;
        }

        internal static void Read(PEReader buff, TableRow[] attrs)
        {
            for (int i = 0; i < attrs.Length; i++)
            {
                attrs[i] = new CustomAttribute(buff);
            }
        }

        internal override void Resolve(PEReader buff)
        {
            parent = buff.GetCodedElement(CIx.HasCustomAttr, parentIx);
            if (parent == null) return;
            parent.AddCustomAttribute(this);
            type = (Method)buff.GetCodedElement(CIx.CustomAttributeType, typeIx);
            byteVal = buff.GetBlob(valIx);
        }

        /*------------------------- public set and get methods --------------------------*/

        public void AddFieldOrProp(string name, Constant val, bool isFld)
        {
            if ((byteVal != null) && !changed) DecodeCustomAttributeBlob();
            if (numNamed == 0)
            {
                names = new string[initSize];
                vals = new Constant[initSize];
                isField = new bool[initSize];
            }
            else if (numNamed >= names.Length)
            {
                string[] tmpNames = names;
                Constant[] tmpVals = vals;
                bool[] tmpField = isField;
                names = new String[names.Length + initSize];
                vals = new Constant[vals.Length + initSize];
                isField = new bool[isField.Length + initSize];
                for (int i = 0; i < numNamed; i++)
                {
                    names[i] = tmpNames[i];
                    vals[i] = tmpVals[i];
                    isField[i] = tmpField[i];
                }
            }
            names[numNamed] = name;
            vals[numNamed] = val;
            isField[numNamed++] = isFld;
            changed = true;
        }

        public Constant[] Args
        {
            get
            {
                if (!changed && (byteVal != null))
                {
                    try
                    {
                        DecodeCustomAttributeBlob();
                    }
                    catch
                    {
                    }
                }
                return argVals;
            }
            set
            {
                argVals = value;
                changed = true;
            }
        }

        public string[] GetNames()
        {
            return names;
        }

        public bool[] GetIsField()
        {
            return isField;
        }
        public Constant[] GetNamedArgs()
        {
            return vals;
        }

        /*----------------------------- internal functions ------------------------------*/

        internal void DecodeCustomAttributeBlob()
        {
            MemoryStream caBlob = new MemoryStream(byteVal);
            BinaryReader blob = new BinaryReader(caBlob, System.Text.Encoding.UTF8);
            if (blob.ReadUInt16() != CustomAttribute.prolog) throw new PEFileException("Invalid Custom Attribute Blob");
            Type[] parTypes = type.GetParTypes();
            argVals = new Constant[parTypes.Length];
            for (int i = 0; i < parTypes.Length; i++)
            {
                Type argType = parTypes[i];
                bool arrayConst = argType is Array;
                if (arrayConst) argType = ((ZeroBasedArray)(parTypes[i])).ElemType();
                bool boxed = argType is SystemClass;
                int eType = argType.GetTypeIndex();
                if (arrayConst)
                {
                    Constant[] elems = new Constant[blob.ReadUInt32()];
                    for (int j = 0; j < elems.Length; j++)
                    {
                        if (boxed)
                        {
                            eType = blob.ReadByte();
                            elems[j] = new BoxedSimpleConst((SimpleConstant)PEReader.ReadConst(eType, blob));
                        }
                        else
                        {
                            elems[j] = PEReader.ReadConst(eType, blob);
                        }
                    }
                    argVals[i] = new ArrayConst(elems);
                }
                else if (boxed)
                {
                    argVals[i] = new BoxedSimpleConst((SimpleConstant)PEReader.ReadConst(blob.ReadByte(), blob));
                }
                else
                {
                    argVals[i] = PEReader.ReadConst(eType, blob);
                }
            }
            uint numNamed = 0;
            if (blob.BaseStream.Position != byteVal.Length)
                numNamed = blob.ReadUInt16();
            if (numNamed > 0)
            {
                names = new string[numNamed];
                vals = new Constant[numNamed];
                isField = new bool[numNamed];
                for (int i = 0; i < numNamed; i++)
                {
                    isField[i] = blob.ReadByte() == 0x53;
                    int eType = blob.ReadByte();
                    names[i] = blob.ReadString();
                    vals[i] = PEReader.ReadConst(eType, blob);
                }
            }
        }

        internal void AddFieldOrProps(string[] names, Constant[] vals, bool[] isField)
        {
            this.names = names;
            this.vals = vals;
            this.isField = isField;
            numNamed = (ushort)names.Length;
        }

        internal void SetBytes(byte[] bytes)
        {
            this.byteVal = bytes;
        }

        internal Method GetCAType()
        {
            return type;
        }

        internal override uint SortKey()
        {
            return (parent.Row << MetaData.CIxShiftMap[(uint)CIx.HasCustomAttr])
                | parent.GetCodedIx(CIx.HasCustomAttr);
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(tabIx, this);
            type.BuildMDTables(md);
            // more adding to tables if data is not bytes
            if (changed || (byteVal == null))
            {
                MemoryStream str = new MemoryStream();
                BinaryWriter bw = new BinaryWriter(str);
                bw.Write((ushort)1);
                if (argVals != null)
                {
                    for (int i = 0; i < argVals.Length; i++)
                    {
                        argVals[i].Write(bw);
                    }
                }
                bw.Write(numNamed);
                for (int i = 0; i < numNamed; i++)
                {
                    if (isField[i]) bw.Write(Field.FieldTag);
                    else bw.Write(Property.PropertyTag);
                    bw.Write(vals[i].GetTypeIndex());
                    bw.Write(names[i]);  // check this is the right format!!!
                    vals[i].Write(bw);
                }
                byteVal = str.ToArray();
            }
            valIx = md.AddToBlobHeap(byteVal);
        }

        internal static uint Size(MetaData md)
        {
            return md.CodedIndexSize(CIx.HasCustomAttr) + md.CodedIndexSize(CIx.CustomAttributeType) + md.BlobIndexSize();
        }

        internal sealed override void Write(PEWriter output)
        {
            output.WriteCodedIndex(CIx.HasCustomAttr, parent);
            output.WriteCodedIndex(CIx.CustomAttributeType, type);
            output.BlobIndex(valIx);
        }

    }


}