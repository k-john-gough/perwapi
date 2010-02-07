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
    /// Descriptor for a field of a class
    /// </summary>
    public abstract class Field : Member
    {
        internal static readonly byte FieldTag = 0x6;

        protected Type type;

        /*-------------------- Constructors ---------------------------------*/

        internal Field(string pfName, Type pfType, Class paren)
            : base(pfName, paren)
        {
            type = pfType;
        }

        internal override void Resolve(PEReader buff)
        {
            if (type == null)
            {
                buff.currentClassScope = parent;
                type = buff.GetFieldType(sigIx);
                buff.currentClassScope = null;
            }
        }

        /*------------------------- public set and get methods --------------------------*/

        /// <summary>
        /// Get the type of this field
        /// </summary>
        /// <returns>Type descriptor for this field</returns>
        public Type GetFieldType() { return type; }

        /// <summary>
        /// Set the type of this field
        /// </summary>
        /// <param name="ty">The type of the field</param>
        public void SetFieldType(Type ty) { type = ty; }

        /*----------------------------- internal functions ------------------------------*/

        internal sealed override void BuildSignatures(MetaDataOut md)
        {
            MemoryStream sig = new MemoryStream();
            sig.WriteByte(FieldTag);
            type.TypeSig(sig);
            sigIx = md.AddToBlobHeap(sig.ToArray());
            done = false;
        }

        internal override string NameString()
        {
            return parent.NameString() + "." + name;
        }

        internal override void WriteType(CILWriter output)
        {
            type.WriteType(output);
            output.Write(" ");
            parent.WriteName(output);
            output.Write("::" + name);
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a field defined in a class of an assembly/module
    /// </summary>
    public class FieldDef : Field
    {
        //private static readonly uint PInvokeImpl = 0x2000;
        private static readonly ushort HasFieldMarshal = 0x1000;
        private static readonly ushort HasFieldRVA = 0x100;
        private static readonly ushort HasDefault = 0x8000;
        private static readonly ushort NoFieldMarshal = 0xEFFF;
        private static readonly ushort NoFieldRVA = 0xFEFF;
        private static readonly ushort NoDefault = 0x7FFF;

        internal FieldRef refOf;
        DataConstant initVal;
        Constant constVal;
        NativeType marshalType;
        ushort flags;
        bool hasOffset = false;
        uint offset;

        /*-------------------- Constructors ---------------------------------*/

        internal FieldDef(string name, Type fType, ClassDef paren)
            : base(name, fType, paren)
        {
            tabIx = MDTable.Field;
        }

        internal FieldDef(FieldAttr attrSet, string name, Type fType, ClassDef paren)
            : base(name, fType, paren)
        {
            flags = (ushort)attrSet;
            tabIx = MDTable.Field;
        }

        internal FieldDef(FieldAttr attrSet, string name, Type fType, ClassSpec paren)
            : base(name, fType, paren)
        {
            flags = (ushort)attrSet;
            tabIx = MDTable.Field;
        }

        internal FieldDef(PEReader buff)
            : base(null, null, null)
        {
            flags = buff.ReadUInt16();
            name = buff.GetString();
            sigIx = buff.GetBlobIx();
            tabIx = MDTable.Field;
        }

        internal static void Read(PEReader buff, TableRow[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
                fields[i] = new FieldDef(buff);
        }

        internal static void GetFieldRefs(PEReader buff, uint num, ClassRef parent)
        {
            for (int i = 0; i < num; i++)
            {
                uint flags = buff.ReadUInt16();
                string name = buff.GetString();
                uint sigIx = buff.GetBlobIx();
                if ((flags & (uint)FieldAttr.Public) == (uint)FieldAttr.Public)
                {
                    if (parent.GetField(name) == null)
                    {
                        //Console.WriteLine(parent.NameString());
                        buff.currentClassScope = parent;
                        FieldRef fRef = new FieldRef(parent, name, buff.GetFieldType(sigIx));
                        buff.currentClassScope = null;
                        parent.AddToFieldList(fRef);
                    }
                }
            }
        }

        internal void Resolve(PEReader buff, uint fIx)
        {
            /*
            if ((flags & HasFieldMarshal) != 0) 
              marshalType = FieldMarshal.FindMarshalType(buff,this,
                buff.MakeCodedIndex(CIx.HasFieldMarshal,MDTable.Field,fIx));
            if ((flags & HasFieldRVA) != 0)
              initVal = FieldRVA.FindValue(buff,this,fIx);
            if ((flags & HasDefault) != 0)
              constVal = ConstantElem.FindConst(buff,this,
                buff.MakeCodedIndex(CIx.HasConstant,MDTable.Field,fIx));
            long offs = FieldLayout.FindLayout(buff,this,fIx);
            if (offs > -1){
              hasOffset = true;
              offset = (uint)offs;
            }
            */
            buff.currentClassScope = parent;
            type = buff.GetFieldType(sigIx);
            buff.currentClassScope = null;
        }

        /*------------------------- public set and get methods --------------------------*/

        /// <summary>
        /// Add an attribute(s) to this field
        /// </summary>
        /// <param name="fa">the attribute(s) to be added</param>
        public void AddFieldAttr(FieldAttr fa)
        {
            flags |= (ushort)fa;
        }
        public void SetFieldAttr(FieldAttr fa)
        {
            flags = (ushort)fa;
        }
        public FieldAttr GetFieldAttr()
        {
            return (FieldAttr)flags;
        }

        /// <summary>
        /// Add a value for this field
        /// </summary>
        /// <param name="val">the value for the field</param>
        public void AddValue(Constant val)
        {
            flags |= HasDefault;
            constVal = val;
        }

        /// <summary>
        /// Retrieve the initial value for this field
        /// </summary>
        /// <returns>initial value</returns>
        public Constant GetValue() { return constVal; }

        /// <summary>
        /// Remove the initial value from this field
        /// </summary>
        public void RemoveValue()
        {
            constVal = null;
            flags &= NoDefault;
        }

        /// <summary>
        /// Add an initial value for this field (at dataLabel) (.data)
        /// </summary>
        /// <param name="val">the value for the field</param>
        public void AddDataValue(DataConstant val)
        {
            flags |= HasFieldRVA;
            initVal = val;
        }

        /// <summary>
        /// Get the value for this data constant
        /// </summary>
        /// <returns></returns>
        public DataConstant GetDataValue()
        {
            return initVal;
        }

        /// <summary>
        /// Delete the value of this data constant
        /// </summary>
        public void RemoveDataValue()
        {
            initVal = null;
            flags &= NoFieldRVA;
        }

        /// <summary>
        /// Set the offset of the field.  Used for sequential or explicit classes.
        /// (.field [offs])
        /// </summary>
        /// <param name="offs">field offset</param>
        public void SetOffset(uint offs)
        {
            offset = offs;
            hasOffset = true;
        }

        /// <summary>
        /// Return the offset for this data constant
        /// </summary>
        /// <returns></returns>
        public uint GetOffset() { return offset; }

        /// <summary>
        /// Delete the offset of this data constant
        /// </summary>
        public void RemoveOffset() { hasOffset = false; }

        /// <summary>
        /// Does this data constant have an offset?
        /// </summary>
        public bool HasOffset() { return hasOffset; }

        /// <summary>
        /// Set the marshalling info for a field
        /// </summary>
        /// <param name="mType"></param>
        public void SetMarshalType(NativeType mType)
        {
            flags |= HasFieldMarshal;
            marshalType = mType;
        }
        public NativeType GetMarshalType() { return marshalType; }
        public void RemoveMarshalType() { marshalType = null; flags &= NoFieldMarshal; }


        /// <summary>
        /// Get the FieldRef equivalent to this FieldDef.  Assumes that
        /// one already exists.
        /// </summary>
        /// <returns>FieldRef for this FieldDef</returns>
        public FieldRef RefOf() { return refOf; }

        /// <summary>
        /// Create the FieldRef equivalent to this FieldDef.  If one does not
        /// exist then create it.
        /// </summary>
        /// <returns>FieldRef for this FieldDef</returns>
        public FieldRef MakeRefOf()
        {
            if (refOf != null) return refOf;
            ClassRef parRef = ((ClassDef)parent).MakeRefOf();
            refOf = parRef.GetField(name);
            if (refOf == null)
            {
                Type refType;
                if (type is ClassDef)
                {
                    refType = ((ClassDef)type).MakeRefOf();
                }
                else
                {
                    refType = type;
                }
                refOf = new FieldRef(parRef, name, refType);
                refOf.defOf = this;
            }
            return refOf;
        }

        /*------------------------- internal functions --------------------------*/

        internal PEFile GetScope()
        {
            return ((ClassDef)parent).GetScope();
        }

        internal void ChangeRefsToDefs(ClassDef newPar, ClassDef[] oldTypes)
        {
            parent = newPar;
            bool changeType = false;
            for (int i = 0; i < oldTypes.Length && !changeType; i++)
            {
                if (type == oldTypes[i])
                    type = newPar;
            }
        }

        internal override bool isDef() { return true; }

        internal void SetParent(ClassDef paren) { parent = paren; }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.Field, this);
            nameIx = md.AddToStringsHeap(name);
            if (!type.isDef()) type.BuildMDTables(md);
            if (initVal != null)
            {
                FieldRVA rva = new FieldRVA(this, initVal);
                rva.BuildMDTables(md);
            }
            if (constVal != null)
            {
                ConstantElem constElem = new ConstantElem(this, constVal);
                constElem.BuildMDTables(md);
            }
            if (hasOffset)
            {
                FieldLayout layout = new FieldLayout(this, offset);
                layout.BuildMDTables(md);
            }
            if (marshalType != null)
            {
                FieldMarshal marshalInfo = new FieldMarshal(this, marshalType);
                marshalInfo.BuildMDTables(md);
            }
        }

        internal sealed override void BuildCILInfo(CILWriter output)
        {
            if (!type.isDef()) type.BuildCILInfo(output);
        }

        internal static uint Size(MetaData md)
        {
            return 2 + md.StringsIndexSize() + md.BlobIndexSize();
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write(flags);
            output.StringsIndex(nameIx);
            output.BlobIndex(sigIx);
        }

        internal override void Write(CILWriter output)
        {
            output.Write("  .field ");
            if (hasOffset)
            {
                output.Write("[ {0} ] ", offset);
            }
            WriteFlags(output, flags);
            if (marshalType != null)
            {
                output.Write("marshal ");
                marshalType.Write(output);
            }
            type.WriteType(output);
            output.Write(" " + name);
            if (initVal != null)
            {
                initVal.Write(output);
            }
            else if (constVal != null)
            {
                constVal.Write(output);
            }
            output.WriteLine();
        }

        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.HasConstant): return 0;
                case (CIx.HasCustomAttr): return 1;
                case (CIx.HasFieldMarshal): return 0;
                case (CIx.MemberForwarded): return 0;
            }
            return 0;
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a field of a class defined in another assembly/module
    /// </summary>
    public class FieldRef : Field
    {
        internal FieldDef defOf;

        /*-------------------- Constructors ---------------------------------*/

        internal FieldRef(Class paren, string name, Type fType)
            : base(name, fType, paren)
        {
            parent = paren;
        }

        internal FieldRef(uint parenIx, string name, uint sigIx)
            : base(name, null, null)
        {
            parentIx = parenIx;
            this.name = name;
            this.sigIx = sigIx;
        }

        internal override Member ResolveParent(PEReader buff)
        {
            if (parent != null) return this;
            MetaDataElement paren = buff.GetCodedElement(CIx.MemberRefParent, parentIx);
            //Console.WriteLine("parentIx = " + parentIx);
            //Console.WriteLine("paren = " + paren);
            if (paren is ClassDef)
                return ((ClassDef)paren).GetField(this.name);
            //if (paren is ClassSpec)
            // paren = ((ClassSpec)paren).GetParent();
            if (paren is ReferenceScope)
                parent = ((ReferenceScope)paren).GetDefaultClass();
            if (paren is TypeSpec)
                parent = new ConstructedTypeSpec((TypeSpec)paren);
            else
                parent = (Class)paren;
            if (parent != null)
            {
                Field existing = (Field)((Class)parent).GetFieldDesc(name);
                if (existing != null)
                {
                    return existing;
                }
            }
            parent.AddToFieldList(this);
            return this;
        }

        /*------------------------- internal functions --------------------------*/

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(tabIx, this);
            nameIx = md.AddToStringsHeap(name);
            if (type is ClassSpec) md.AddToTable(MDTable.TypeSpec, type);
            if (!type.isDef())
                type.BuildMDTables(md);
            if (parent != null)
            {
                if (parent is ClassSpec) md.AddToTable(MDTable.TypeSpec, parent);
                parent.BuildMDTables(md);
            }
        }

        internal override void BuildCILInfo(CILWriter output)
        {
            parent.BuildCILInfo(output);
        }

        internal static uint Size(MetaData md)
        {
            return md.CodedIndexSize(CIx.MemberRefParent) + md.StringsIndexSize() + md.BlobIndexSize();
        }

        internal sealed override void Write(PEWriter output)
        {
            output.WriteCodedIndex(CIx.MemberRefParent, parent);
            output.StringsIndex(nameIx);
            output.BlobIndex(sigIx);
        }

        internal sealed override uint GetCodedIx(CIx code) { return 6; }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for layout information for a field
    /// </summary>
    public class FieldLayout : MetaDataElement
    {
        FieldDef field;
        uint offset, fieldIx = 0;

        /*-------------------- Constructors ---------------------------------*/

        internal FieldLayout(FieldDef field, uint offset)
        {
            this.field = field;
            this.offset = offset;
            tabIx = MDTable.FieldLayout;
        }

        internal FieldLayout(PEReader buff)
        {
            offset = buff.ReadUInt32();
            fieldIx = buff.GetIndex(MDTable.Field);
            tabIx = MDTable.FieldLayout;
        }

        internal static void Read(PEReader buff, TableRow[] layouts)
        {
            for (int i = 0; i < layouts.Length; i++)
                layouts[i] = new FieldLayout(buff);
        }

        internal sealed override void Resolve(PEReader buff)
        {
            field = (FieldDef)buff.GetElement(MDTable.Field, fieldIx);
            field.SetOffset(offset);
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.FieldLayout, this);
        }

        internal static uint Size(MetaData md)
        {
            return 4 + md.TableIndexSize(MDTable.Field);
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write(offset);
            output.WriteIndex(MDTable.Field, field.Row);
        }

    }
}