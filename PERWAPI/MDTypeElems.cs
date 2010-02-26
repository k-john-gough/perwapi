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
    /// Base class for all IL types
    /// </summary>
    public abstract class Type : MetaDataElement
    {
        protected byte typeIndex;

      /// <summary>
      /// The following is only used for TypeSpecs and ClassSpecs. kjg
      /// </summary>
        internal bool typeSpecAdded = false; // so that MetaDataOut can reset it


        /*-------------------- Constructors ---------------------------------*/

        internal Type(byte tyIx) { typeIndex = tyIx; }

        internal byte GetTypeIndex() { return typeIndex; }

        internal virtual bool SameType(Type tstType)
        {
            return this == tstType;
        }

        internal virtual void TypeSig(MemoryStream str)
        {
            throw new TypeSignatureException(this.GetType().AssemblyQualifiedName +
                " doesn't have a type signature!!");
        }

        public virtual string TypeName()
        {
            return "NoTypeName";
        }

        internal virtual void WriteType(CILWriter output)
        {
            throw new NotYetImplementedException("Writing types for CIL");
        }

        internal virtual void WriteName(CILWriter output)
        {
            WriteType(output);
        }

        internal virtual Type AddTypeSpec(MetaDataOut md)
        {
            if (!isDef()) BuildMDTables(md);
            return this;
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a custom modifier of a type (modopt or modreq)
    /// </summary>
    public class CustomModifiedType : Type
    {
        Type type;
        Class cmodType;

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create a new custom modifier for a type
        /// </summary>
        /// <param name="type">the type to be modified</param>
        /// <param name="cmod">the modifier</param>
        /// <param name="cmodType">the type reference to be associated with the type</param>
        public CustomModifiedType(Type type, CustomModifier cmod, Class cmodType)
            : base((byte)cmod)
        {
            this.type = type;
            this.cmodType = cmodType;
        }

        /*------------------------- public set and get methods --------------------------*/

        public void SetModifiedType(Type modType) { type = modType; }
        public Type GetModifiedType() { return type; }

        public void SetModifingType(Class mod) { cmodType = mod; }
        public Class GetModifingType() { return cmodType; }

        public void SetModifier(CustomModifier cmod) { typeIndex = (byte)cmod; }
        public CustomModifier GetModifier() { return (CustomModifier)typeIndex; }

        /*----------------------------- internal functions ------------------------------*/

        internal override bool SameType(Type tstType)
        {
            if (this == tstType) return true;
            if (tstType is CustomModifiedType)
            {
                CustomModifiedType cmTstType = (CustomModifiedType)tstType;
                return type.SameType(cmTstType.type) &&
                    cmodType.SameType(cmTstType.cmodType);
            }
            return false;
        }
        internal sealed override void TypeSig(MemoryStream str)
        {
            str.WriteByte(typeIndex);
            MetaDataOut.CompressNum(BlobUtil.CompressUInt(cmodType.TypeDefOrRefToken()), str);
            type.TypeSig(str);
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            if (!(cmodType is ClassDef))
                cmodType.BuildMDTables(md);
            if (!(type is ClassDef))
                type.BuildMDTables(md);
        }

    }

    /**************************************************************************/
    internal class Pinned : Type
    {
        internal Pinned() : base((byte)ElementType.Pinned) { }
    }

    /**************************************************************************/
    internal class Sentinel : Type
    {
        internal Sentinel() : base((byte)ElementType.Sentinel) { }
    }

    /**************************************************************************/
    public abstract class TypeSpec : Type
    {

        uint sigIx = 0;
        //internal bool typeSpecAdded = false; // so that MetaDataOut can reset it

        /*-------------------- Constructors ---------------------------------*/

        internal TypeSpec(byte typeIx)
            : base(typeIx)
        {
            tabIx = MDTable.TypeSpec;
        }

        internal static void Read(PEReader buff, TableRow[] specs)
        {
            for (int i = 0; i < specs.Length; i++)
            {
                specs[i] = new UnresolvedTypeSpec(buff, i);
                //specs[i] = buff.GetBlobType(null,null,buff.GetBlobIx());
                //if (specs[i] is GenericParam) {
                //  Console.WriteLine("GenericParam in TypeSpec table at pos " + i);
                //}
            }
        }

        internal override sealed Type AddTypeSpec(MetaDataOut md)
        {
            if (typeSpecAdded) return this;
            md.ConditionalAddTypeSpec(this);
            BuildMDTables(md);
            typeSpecAdded = true;
            return this;
        }

        internal override void BuildSignatures(MetaDataOut md)
        {
            MemoryStream str = new MemoryStream();
            TypeSig(str);
            sigIx = md.AddToBlobHeap(str.ToArray());
            done = false;
        }

        internal static uint Size(MetaData md)
        {
            return md.BlobIndexSize();
        }

        internal sealed override void Write(PEWriter output)
        {
            //Console.WriteLine("Writing the blob index for a TypeSpec");
            output.BlobIndex(sigIx);
        }

        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.TypeDefOrRef): return 2;
                case (CIx.HasCustomAttr): return 13;
                case (CIx.MemberRefParent): return 4;
            }
            return 0;
        }
    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for the Primitive types defined in IL
    /// </summary>
    public class PrimitiveType : TypeSpec
    {
        private string name;
        private int systemTypeIndex;
        internal static int NumSystemTypes = 18;

        public static readonly PrimitiveType Void = new PrimitiveType(0x01, "Void", 0);
        public static readonly PrimitiveType Boolean = new PrimitiveType(0x02, "Boolean", 1);
        public static readonly PrimitiveType Char = new PrimitiveType(0x03, "Char", 2);
        public static readonly PrimitiveType Int8 = new PrimitiveType(0x04, "SByte", 3);
        public static readonly PrimitiveType UInt8 = new PrimitiveType(0x05, "Byte", 4);
        public static readonly PrimitiveType Int16 = new PrimitiveType(0x06, "Int16", 5);
        public static readonly PrimitiveType UInt16 = new PrimitiveType(0x07, "UInt16", 6);
        public static readonly PrimitiveType Int32 = new PrimitiveType(0x08, "Int32", 7);
        public static readonly PrimitiveType UInt32 = new PrimitiveType(0x09, "UInt32", 8);
        public static readonly PrimitiveType Int64 = new PrimitiveType(0x0A, "Int64", 9);
        public static readonly PrimitiveType UInt64 = new PrimitiveType(0x0B, "UInt64", 10);
        public static readonly PrimitiveType Float32 = new PrimitiveType(0x0C, "Single", 11);
        public static readonly PrimitiveType Float64 = new PrimitiveType(0x0D, "Double", 12);
        public static readonly PrimitiveType String = new PrimitiveType(0x0E, "String", 13);
        internal static readonly PrimitiveType Class = new PrimitiveType(0x12);
        public static readonly PrimitiveType TypedRef = new PrimitiveType(0x16, "TypedReference", 14);
        public static readonly PrimitiveType IntPtr = new PrimitiveType(0x18, "IntPtr", 15);
        public static readonly PrimitiveType UIntPtr = new PrimitiveType(0x19, "UIntPtr", 16);
        public static readonly PrimitiveType Object = new PrimitiveType(0x1C, "Object", 17);
        internal static readonly PrimitiveType ClassType = new PrimitiveType(0x50);
        internal static readonly PrimitiveType SZArray = new PrimitiveType(0x1D);
        public static readonly PrimitiveType NativeInt = IntPtr;
        public static readonly PrimitiveType NativeUInt = UIntPtr;
        internal static PrimitiveType[] primitives = {null,Void,Boolean,Char,Int8,UInt8,
                                                         Int16,UInt16,Int32,UInt32,Int64,
                                                         UInt64,Float32,Float64,String};

        /*-------------------- Constructors ---------------------------------*/

        internal PrimitiveType(byte typeIx) : base(typeIx) { }

        internal PrimitiveType(byte typeIx, string name, int STIx)
            : base(typeIx)
        {
            this.name = name;
            this.systemTypeIndex = STIx;
        }

        internal string GetName() { return name; }

        public override string TypeName()
        {
            if (typeIndex == 0x0E) return "System.String";
            return name;
        }

        internal int GetSystemTypeIx() { return systemTypeIndex; }

        internal sealed override void TypeSig(MemoryStream str)
        {
            str.WriteByte(typeIndex);
        }

        internal override void WriteType(CILWriter output)
        {
            //if (typeIndex == 0x0E) {
            //    output.Write("[mscorlib]System.String");
            //} else 
            switch (typeIndex)
            {
                case (0x1C): output.Write("[mscorlib]System.Object"); break;
                case (0x02): output.Write("bool"); break;
                case (0x0C): output.Write("float32"); break;
                case (0x0D): output.Write("float64"); break;
                default: output.Write(name.ToLower()); break;
            }
        }

        internal sealed override bool SameType(Type tstType)
        {
            if (tstType is SystemClass)
                return tstType.SameType(this);
            return this == tstType;
        }

        /* now done in MetaDataOut.WriteTildeStream
        internal static void ClearAddedFlags() {   // KJG 18-April-2005
            for (int i = 0; i < primitives.Length; i++) {
                if (primitives[i] != null) primitives[i].typeSpecAdded = false;
            }
        }
        */
    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a generic parameter for either a class or method. 
    /// </summary>
    public class GenericParam : Type
    {
        private static readonly byte VAR = 0x13;
        private static readonly byte MVAR = 0x1E;
        ushort flags, index, kind = 0;
        uint parentIx, nameIx;
        string name;
        MetaDataElement parent;
        private ArrayList constraints = new ArrayList();
        internal static bool extraField = true;

        // There should only be one GenericParTypeSpec entry 
        // int the metadata for each GenericParam.
        GenericParTypeSpec myTypeSpec;

        /*-------------------- Constructors ---------------------------------*/

        private GenericParam(uint index, byte elemIx)
            : base(elemIx)
        {
            this.index = (ushort)index;
            sortTable = true;
        }

        internal GenericParam(string name, MetaDataElement parent, int index)
            : base(VAR)
        {
            this.name = name;
            this.parent = parent;
            this.index = (ushort)index;
            if (parent is Method) typeIndex = MVAR;
            sortTable = true;
            tabIx = MDTable.GenericParam;
        }

        internal GenericParam(PEReader buff)
            : base(VAR)
        {
            index = buff.ReadUInt16();
            flags = buff.ReadUInt16();
            parentIx = buff.GetCodedIndex(CIx.TypeOrMethodDef);
            name = buff.GetString();
            if (extraField) kind = buff.ReadUInt16();
            sortTable = true;
            tabIx = MDTable.GenericParam;
            // resolve generic param immediately for signature resolution
            parent = buff.GetCodedElement(CIx.TypeOrMethodDef, parentIx);
            if (parent != null)
            {
                if (parent is MethodDef)
                {
                    typeIndex = MVAR;
                    ((MethodDef)parent).AddGenericParam(this);
                }
                else
                {
                    ((ClassDef)parent).AddGenericParam(this);
                }
            }
        }

        internal GenericParam(string name)
            : base(MVAR)
        {
            this.name = name;
            sortTable = true;
            tabIx = MDTable.GenericParam;
        }

        internal static GenericParam AnonMethPar(uint ix)
        {
            return new GenericParam(ix, MVAR);
        }

        internal static GenericParam AnonClassPar(uint ix)
        {
            return new GenericParam(ix, VAR);
        }

        internal static void Read(PEReader buff, TableRow[] gpars)
        {
            for (int i = 0; i < gpars.Length; i++)
                gpars[i] = new GenericParam(buff);
        }

        /*------------------------- public set and get methods --------------------------*/

        /// <summary>
        /// Set the attribute for this generic parameter
        /// </summary>
        /// <param name="attr">the attribute</param>
        public void SetAttribute(GenericParamAttr attr)
        {
            flags = (ushort)attr;
        }

        /// <summary>
        /// Get the attribute for this generic parameter
        /// </summary>
        public GenericParamAttr GetAttribute()
        {
            return (GenericParamAttr)flags;
        }

        /// <summary>
        /// Add a type constraint to this generic parameter
        /// </summary>
        /// <param name="cType">class constraining the parameter type</param>
        public void AddConstraint(Class cType)
        {
            constraints.Add(cType);
        }

        /// <summary>
        /// Remove a constraint from this generic parameter
        /// </summary>
        /// <param name="cType">class type of constraint</param>
        public void RemoveConstraint(Class cType)
        {
            for (int i = 0; i < constraints.Count; i++)
            {
                if (constraints[i] == cType)
                {
                    constraints.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Return a constraint from the list
        /// </summary>
        /// <param name="i">constraint index</param>
        /// <returns></returns>
        public Class GetConstraint(int i)
        {
            return (Class)constraints[i];
        }

        /// <summary>
        /// Get the number of constrains on this GenericParam
        /// </summary>
        /// <returns></returns>
        public int GetConstraintCount()
        {
            return constraints.Count;
        }

        /// <summary>
        /// Get the name of this generic parameter
        /// </summary>
        /// <returns>generic parameter name</returns>
        public string GetName() { return name; }

        public MetaDataElement GetParent() { return parent; }

        public Class[] GetClassConstraints()
        {
            return (Class[])constraints.ToArray(typeof(Class)); // KJG 20-May-2005
        }

        /*----------------------------- internal functions ------------------------------*/

        internal uint Index
        {
            get { return index; }
            set { index = (ushort)value; }
        }

        internal void SetClassParam(Class paren, int ix)
        {
            typeIndex = VAR;
            parent = paren;
            index = (ushort)ix;
        }

        internal void SetMethParam(Method paren, int ix)
        {
            typeIndex = MVAR;
            parent = paren;
            index = (ushort)ix;
        }

        internal void CheckParent(MethodDef paren, PEReader buff)
        {
            if (paren == buff.GetCodedElement(CIx.TypeOrMethodDef, parentIx))
            {
                parent = paren;
                paren.InsertGenericParam(this);
            }
        }

        internal override void TypeSig(MemoryStream str)
        {
            str.WriteByte(typeIndex);
            str.WriteByte((byte)index);
        }

        internal static uint Size(MetaData md)
        {
            if (extraField)
                return 6 + md.CodedIndexSize(CIx.TypeOrMethodDef) + md.StringsIndexSize();
            else
                return 4 + md.CodedIndexSize(CIx.TypeOrMethodDef) + md.StringsIndexSize();
        }

        internal override Type AddTypeSpec(MetaDataOut md)
        {
          if (this.myTypeSpec == null) {
            this.myTypeSpec = new GenericParTypeSpec(this);
            md.AddToTable(MDTable.TypeSpec, this.myTypeSpec);
          }
          return this.myTypeSpec;
        }

        

        internal override uint SortKey()
        {
            return (parent.Row << MetaData.CIxShiftMap[(uint)CIx.TypeOrMethodDef])
                | parent.GetCodedIx(CIx.TypeOrMethodDef);
        }

        internal override void BuildTables(MetaDataOut md)
        {
            if (parent is MethodRef || parent is ClassRef) return; // don't add it - fix by CK
            md.AddToTable(MDTable.GenericParam, this);
            nameIx = md.AddToStringsHeap(name);
            for (int i = 0; i < constraints.Count; i++)
            {
                Class cClass = (Class)constraints[i];
                constraints[i] = new GenericParamConstraint(this, cClass);
                if (cClass is ClassRef) cClass.BuildMDTables(md);
                // Fix by CK - should be BuildTables too??
                if (cClass is ClassSpec) md.ConditionalAddTypeSpec(cClass);
            }
        }

        internal override void BuildCILInfo(CILWriter output)
        {
            for (int i = 0; i < constraints.Count; i++)
            {
                Class cClass = (Class)constraints[i];
                if (!cClass.isDef())
                {
                    cClass.BuildCILInfo(output);
                }
            }
        }

        internal void AddConstraints(MetaDataOut md)
        {
            for (int i = 0; i < constraints.Count; i++)
            {
                md.AddToTable(MDTable.GenericParamConstraint, (GenericParamConstraint)constraints[i]);
            }
        }

        internal override void Write(PEWriter output)
        {
            output.Write(index);
            output.Write(flags);
            output.WriteCodedIndex(CIx.TypeOrMethodDef, parent);
            output.StringsIndex(nameIx);
            if (extraField) output.Write(kind);
        }

    }

    /**************************************************************************/
    internal class UnresolvedTypeSpec : TypeSpec
    {
        uint blobIx;

        internal UnresolvedTypeSpec(PEReader buff, int i)
            : base(0)
        {
            blobIx = buff.GetBlobIx();
            Row = (uint)i + 1;
            this.unresolved = true;
        }

        internal override void Resolve(PEReader buff)
        {
            buff.InsertInTable(MDTable.TypeSpec, Row, buff.GetBlobType(blobIx));
            this.unresolved = false;
        }


    }

    /**************************************************************************/
    /// <summary>
    /// Wrapper for Generic Parameter of TypeSpec type.
    /// </summary> 
    public class GenericParTypeSpec : TypeSpec
    {
        GenericParam gPar;
        bool isClassPar;
        uint index;

        internal GenericParTypeSpec(GenericParam gPar)
            : base(gPar.GetTypeIndex())
        {
            this.gPar = gPar;
        }

        internal GenericParTypeSpec(int gpTypeIx, uint ix)
            : base((byte)gpTypeIx)
        {
            isClassPar = gpTypeIx == (int)ElementType.Var;
            index = ix;
        }

        internal GenericParam GetGenericParam(MethodDef meth)
        {
            if (gPar == null)
            {
                if (isClassPar)
                {
                    ClassDef methClass = (ClassDef)meth.GetParent();
                    gPar = methClass.GetGenericParam((int)index);
                }
                else
                {
                    gPar = meth.GetGenericParam((int)index);
                }
            }
            return gPar;
        }

        internal override void TypeSig(MemoryStream str)
        {
            gPar.TypeSig(str);
        }

    }

    /**************************************************************************/
    /// <summary>
    /// The IL Array type: there are two sub-classes --
    /// BoundArrays, possibly multi dimensional arrays with bounds.
    /// ZeroBasedArrays, built-in 1-D arrays of the CLR
    /// </summary>
    public abstract class Array : TypeSpec
    {
        /// <summary>
        /// The element type of the array
        /// </summary>
        protected Type elemType;

        /*-------------------- Constructors ---------------------------------*/

        internal Array(Type eType, byte TypeId)
            : base(TypeId)
        {
            elemType = eType;
            tabIx = MDTable.TypeSpec;
        }

        public Type ElemType() { return elemType; }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            if (!(elemType is ClassDef))
                elemType.BuildMDTables(md);
        }

        internal sealed override void BuildCILInfo(CILWriter output)
        {
            if (!(elemType is ClassDef))
                elemType.BuildCILInfo(output);
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Arrays with one or more dimensions, with explicit bounds
    /// </summary>
    public class BoundArray : Array
    {
        int[] lowerBounds;
        int[] sizes;
        uint numDims;

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create a new multi dimensional array type 
        /// eg. elemType[1..5,3..10,5,,] would be 
        /// new BoundArray(elemType,5,[1,3,0],[5,10,4])
        /// </summary>
        /// <param name="elementType">the type of the elements</param>
        /// <param name="dimensions">the number of dimensions</param>
        /// <param name="loBounds">lower bounds of dimensions</param>
        /// <param name="upBounds">upper bounds of dimensions</param>
        public BoundArray(
            Type elementType, 
            int dimensions, 
            int[] loBounds,
            int[] upBounds) : base(elementType, 0x14)
        {
            numDims = (uint)dimensions;
            lowerBounds = loBounds;

            if (loBounds.Length > dimensions)
                throw new TypeSignatureException("Array cannot have more bounds than rank");
            if (upBounds != null)
            {
                if (upBounds.Length > loBounds.Length)
                    throw new TypeSignatureException("Array cannot have more upper than lower bounds");
                sizes = new int[upBounds.Length];
                for (int i = 0; i < upBounds.Length; i++)
                {
                    sizes[i] = upBounds[i] - loBounds[i] + 1;
                }
            }
        }

        /// <summary>
        /// Create a new multi dimensional array type with low bounds
        /// specified but no sizes specified.  C# arrays T[,] do this
        /// with implicit low bounds of zero, but no sizes
        /// </summary>
        /// <param name="elementType">the type of the elements</param>
        /// <param name="dimensions">the number of dimensions</param>
        /// <param name="bounds">the low bounds of the dimensions</param>
        public BoundArray(Type elementType, int dimensions, int[] bounds)
            : base(elementType, 0x14)
        {
            if (bounds.Length > dimensions)
                throw new TypeSignatureException("Array cannot have more bounds than rank");
            numDims = (uint)dimensions;
            lowerBounds = bounds;
        }

        /// <summary>
        /// Create a new multi dimensional array type 
        /// eg. elemType[,,] would be new BoundArray(elemType,3)
        /// </summary>
        /// <param name="elementType">the type of the elements</param>
        /// <param name="dimensions">the number of dimensions</param>
        public BoundArray(Type elementType, int dimensions)
            : base(elementType, 0x14)
        {
            numDims = (uint)dimensions;
        }

        internal override bool SameType(Type tstType)
        {
            if (this == tstType) return true;
            if (!(tstType is BoundArray)) return false;
            BoundArray bArray = (BoundArray)tstType;
            if (elemType.SameType(bArray.ElemType()))
                return SameBounds(numDims, lowerBounds, sizes);
            return false;
        }

        internal bool SameBounds(uint dims, int[] lbounds, int[] sizs)
        {
            if (dims != numDims) return false;
            if (lowerBounds != null)
            {
                if ((lbounds == null) || (lowerBounds.Length != lbounds.Length)) return false;
                for (int i = 0; i < lowerBounds.Length; i++)
                    if (lowerBounds[i] != lbounds[i]) return false;
            }
            else
                if (lbounds != null) return false;
            if (sizes != null)
            {
                if ((sizs == null) || (sizes.Length != sizs.Length)) return false;
                for (int i = 0; i < sizes.Length; i++)
                    if (sizes[i] != sizs[i]) return false;
            }
            else
                if (sizs != null) return false;
            return true;
        }

        internal sealed override void TypeSig(MemoryStream str)
        {
            str.WriteByte(typeIndex);
            elemType.TypeSig(str);
            MetaDataOut.CompressNum(BlobUtil.CompressUInt(numDims), str);
            if ((sizes != null) && (sizes.Length > 0))
            {
                MetaDataOut.CompressNum(BlobUtil.CompressUInt((uint)sizes.Length), str);
                for (int i = 0; i < sizes.Length; i++)
                {
                    MetaDataOut.CompressNum(BlobUtil.CompressUInt((uint)sizes[i]), str);
                }
            }
            else str.WriteByte(0);
            if ((lowerBounds != null) && (lowerBounds.Length > 0))
            {
                MetaDataOut.CompressNum(BlobUtil.CompressUInt((uint)lowerBounds.Length), str);
                for (int i = 0; i < lowerBounds.Length; i++)
                {
                    MetaDataOut.CompressNum(BlobUtil.CompressInt(lowerBounds[i]), str);
                }
            }
            else str.WriteByte(0);
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Single dimensional array with zero lower bound
    /// </summary>
    public class ZeroBasedArray : Array
    {

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create a new array  -   elementType[]
        /// </summary>
        /// <param name="elementType">the type of the array elements</param>
        public ZeroBasedArray(Type elementType) : base(elementType, (byte)ElementType.SZArray) { }

        internal sealed override void TypeSig(MemoryStream str)
        {
            str.WriteByte(typeIndex);
            elemType.TypeSig(str);
        }

        internal override bool SameType(Type tstType)
        {
            if (this == tstType) return true;
            if (!(tstType is ZeroBasedArray)) return false;
            //return elemType == ((ZeroBasedArray)tstType).ElemType();
            return elemType.SameType(((ZeroBasedArray)tstType).ElemType());
        }

        internal override void WriteType(CILWriter output)
        {
            elemType.WriteType(output);
            output.Write("[]");
        }



    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a FunctionPointer type
    /// </summary>
    /// 
    public class MethPtrType : TypeSpec
    {
        // MethPtrType == FNPTR
        Method meth;
        MethSig mSig;

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create a new function pointer type
        /// </summary>
        /// <param name="meth">the function to be referenced</param>
        public MethPtrType(Method meth)
            : base((byte)ElementType.FnPtr)
        {
            this.meth = meth;
        }

        internal MethPtrType(MethSig msig)
            : base((byte)ElementType.FnPtr)
        {
            mSig = msig;
        }

        internal sealed override void TypeSig(MemoryStream str)
        {
            str.WriteByte(typeIndex);
            if (meth == null)
                mSig.TypeSig(str);
            else
                meth.TypeSig(str);
        }

        internal override bool SameType(Type tstType)
        {
            if (this == tstType) return true;
            if (tstType is MethPtrType)
            {
                MethPtrType mpType = (MethPtrType)tstType;

            }
            return false;
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            Type[] types = meth.GetParTypes();
            if (types != null)
                for (int i = 0; i < types.Length; i++)
                    types[i].BuildMDTables(md);
            types = meth.GetOptParTypes();
            if (types != null)
                for (int i = 0; i < types.Length; i++)
                    types[i].BuildMDTables(md);
        }

        internal sealed override void BuildCILInfo(CILWriter output)
        {
            Type[] types = meth.GetParTypes();
            if (types != null)
                for (int i = 0; i < types.Length; i++)
                    types[i].BuildCILInfo(output);
            types = meth.GetOptParTypes();
            if (types != null)
                for (int i = 0; i < types.Length; i++)
                    types[i].BuildCILInfo(output);
        }

        /*    internal sealed override void BuildSignatures(MetaDataOut md) {
              if (sigIx == 0) {
                MemoryStream sig = new MemoryStream();
                TypeSig(sig);
                sigIx = md.AddToBlobHeap(sig.ToArray());
              }
              done = false;
            }
            */

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for an pointer (type * or type &)
    /// </summary>
    public abstract class PtrType : TypeSpec
    {
        protected Type baseType;

        /*-------------------- Constructors ---------------------------------*/

        internal PtrType(Type bType, byte typeIx)
            : base(typeIx)
        {
            baseType = bType;
        }

        public Type GetBaseType() { return baseType; }

        internal sealed override void TypeSig(MemoryStream str)
        {
            str.WriteByte(typeIndex);
            baseType.TypeSig(str);
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            if (!(baseType is ClassDef))
                baseType.BuildMDTables(md);
        }

        internal sealed override void BuildCILInfo(CILWriter output)
        {
            if (!(baseType is ClassDef))
                baseType.BuildCILInfo(output);
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a managed pointer (type & or byref)
    /// </summary>
    public class ManagedPointer : PtrType
    {  // <type> & (BYREF)  

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create new managed pointer to baseType
        /// </summary>
        /// <param name="bType">the base type of the pointer</param>
        public ManagedPointer(Type baseType) : base(baseType, 0x10) { }
    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for an unmanaged pointer (type *)
    /// </summary>
    public class UnmanagedPointer : PtrType
    { // PTR

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create a new unmanaged pointer to baseType
        /// </summary>
        /// <param name="baseType">the base type of the pointer</param>
        public UnmanagedPointer(Type baseType) : base(baseType, 0x0F) { }

    }

}