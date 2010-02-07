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
    /// Base class for Event and Property descriptors
    /// </summary>
    public abstract class Feature : MetaDataElement
    {
        private static readonly int INITSIZE = 5;
        private static readonly ushort specialName = 0x200;
        private static readonly ushort rtsSpecialName = 0x400;
        private static readonly ushort noSpecialName = 0xFDFF;
        private static readonly ushort noRTSSpecialName = 0xFBFF;

        protected ClassDef parent;
        protected ushort flags = 0;
        protected string name;
        protected int tide = 0;
        protected uint nameIx;
        protected MethodSemantics[] methods = new MethodSemantics[INITSIZE];

        /*-------------------- Constructors ---------------------------------*/

        internal Feature(string name, ClassDef par)
        {
            parent = par;
            this.name = name;
        }

        internal Feature() { }

        internal static string[] GetFeatureNames(PEReader buff, MDTable tabIx, MDTable mapTabIx,
            ClassDef theClass, uint classIx)
        {
            buff.SetElementPosition(mapTabIx, 0);
            uint start = 0, end = 0, i = 0;
            for (; (i < buff.GetTableSize(tabIx)) && (start == 0); i++)
            {
                if (buff.GetIndex(MDTable.TypeDef) == classIx)
                {
                    start = buff.GetIndex(tabIx);
                }
            }
            if (start == 0) return null;
            if (i < buff.GetTableSize(mapTabIx))
            {
                uint junk = buff.GetIndex(MDTable.TypeDef);
                end = buff.GetIndex(tabIx);
            }
            else
                end = buff.GetTableSize(tabIx);
            if (tabIx == MDTable.Event)
                theClass.eventIx = start;
            else
                theClass.propIx = start;
            string[] names = new string[end - start];
            buff.SetElementPosition(tabIx, start);
            for (i = start; i < end; i++)
            {
                uint junk = buff.ReadUInt16();
                names[i] = buff.GetString();
                if (tabIx == MDTable.Event)
                    junk = buff.GetCodedIndex(CIx.TypeDefOrRef);
                else
                    junk = buff.GetBlobIx();
            }
            return names;
        }


        /*------------------------- public set and get methods --------------------------*/

        /// <summary>
        /// Set the specialName attribute for this Event or Property
        /// </summary>
        public void SetSpecialName() { flags |= specialName; }
        public bool HasSpecialName() { return (flags & specialName) != 0; }
        public void ClearSpecialName() { flags &= noSpecialName; }

        /// <summary>
        /// Set the RTSpecialName attribute for this Event or Property
        /// </summary>
        public void SetRTSpecialName() { flags |= rtsSpecialName; }
        public bool HasRTSSpecialName() { return (flags & rtsSpecialName) != 0; }
        public void ClearRTSSpecialName() { flags &= noRTSSpecialName; }

        public string Name() { return name; }
        public void SetName(string nam) { name = nam; }

        internal void AddMethod(MethodSemantics meth)
        {
            if (tide == methods.Length)
            {
                MethodSemantics[] mTmp = methods;
                methods = new MethodSemantics[tide * 2];
                for (int i = 0; i < tide; i++)
                {
                    methods[i] = mTmp[i];
                }
            }
            methods[tide++] = meth;
        }

        public void AddMethod(MethodDef meth, MethodType mType)
        {
            AddMethod(new MethodSemantics(mType, meth, this));
        }

        public MethodDef GetMethod(MethodType mType)
        {
            for (int i = 0; i < tide; i++)
            {
                if (methods[i].GetMethodType() == mType)
                    return methods[i].GetMethod();
            }
            return null;
        }

        public void RemoveMethod(MethodDef meth)
        {
            bool found = false;
            for (int i = 0; i < tide; i++)
            {
                if (found)
                    methods[i - 1] = methods[i];
                else if (methods[i].GetMethod() == meth)
                    found = true;
            }
        }

        public void RemoveMethod(MethodType mType)
        {
            bool found = false;
            for (int i = 0; i < tide; i++)
            {
                if (found)
                    methods[i - 1] = methods[i];
                else if (methods[i].GetMethodType() == mType)
                    found = true;
            }
        }

        internal void SetParent(ClassDef paren)
        {
            parent = paren;
        }

        internal ClassDef GetParent()
        {
            return parent;
        }
    }

    /*****************************************************************************/
    /// <summary>
    /// Descriptor for an event
    /// </summary>
    public class Event : Feature
    {
        Type eventType;
        uint typeIx = 0;

        /*-------------------- Constructors ---------------------------------*/

        internal Event(string name, Type eType, ClassDef parent)
            : base(name, parent)
        {
            eventType = eType;
            tabIx = MDTable.Event;
        }

        internal Event(PEReader buff)
        {
            flags = buff.ReadUInt16();
            name = buff.GetString();
            typeIx = buff.GetCodedIndex(CIx.TypeDefOrRef);
            tabIx = MDTable.Event;
        }

        internal static void Read(PEReader buff, TableRow[] events)
        {
            for (int i = 0; i < events.Length; i++)
                events[i] = new Event(buff);
        }

        internal static string[] ReadNames(PEReader buff, ClassDef theClass, uint classIx)
        {
            return Feature.GetFeatureNames(buff, MDTable.Event, MDTable.EventMap, theClass, classIx);
        }

        internal override void Resolve(PEReader buff)
        {
            eventType = (Type)buff.GetCodedElement(CIx.TypeDefOrRef, typeIx);
        }

        /*------------------------- public set and get methods --------------------------*/

        public Type GetEventType() { return eventType; }

        /*----------------------------- internal functions ------------------------------*/

        internal void ChangeRefsToDefs(ClassDef newType, ClassDef[] oldTypes)
        {
            throw new NotYetImplementedException("Merge for Events");
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.Event, this);
            nameIx = md.AddToStringsHeap(name);
            eventType.BuildMDTables(md);
            for (int i = 0; i < tide; i++)
            {
                methods[i].BuildMDTables(md);
            }
        }

        internal override void BuildCILInfo(CILWriter output)
        {
            eventType.BuildCILInfo(output);
        }

        internal static uint Size(MetaData md)
        {
            return 2 + md.StringsIndexSize() + md.CodedIndexSize(CIx.TypeDefOrRef);
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write(flags);
            output.StringsIndex(nameIx);
            output.WriteCodedIndex(CIx.TypeDefOrRef, eventType);
        }

        internal override void Write(CILWriter output)
        {
            throw new NotYetImplementedException("Write CIL for event");
        }

        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.HasCustomAttr): return 10;
                case (CIx.HasSemantics): return 0;
            }
            return 0;
        }
    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for the Property of a class
    /// </summary>
    public class Property : Feature
    {
        internal static readonly byte PropertyTag = 0x8;
        Constant constVal;
        uint typeBlobIx = 0;
        Type[] parList;
        Type returnType;
        uint numPars = 0;

        /*-------------------- Constructors ---------------------------------*/

        internal Property(string name, Type retType, Type[] pars, ClassDef parent)
            : base(name, parent)
        {
            returnType = retType;
            parList = pars;
            if (pars != null) numPars = (uint)pars.Length;
            tabIx = MDTable.Property;
        }

        internal Property(PEReader buff)
        {
            flags = buff.ReadUInt16();
            name = buff.GetString();
            typeBlobIx = buff.GetBlobIx();
            tabIx = MDTable.Property;
        }

        internal static void Read(PEReader buff, TableRow[] props)
        {
            for (int i = 0; i < props.Length; i++)
                props[i] = new Property(buff);
        }

        internal static string[] ReadNames(PEReader buff, ClassDef theClass, uint classIx)
        {
            return Feature.GetFeatureNames(buff, MDTable.Property, MDTable.PropertyMap, theClass, classIx);
        }

        internal sealed override void Resolve(PEReader buff)
        {
            buff.ReadPropertySig(typeBlobIx, this);
        }

        /// <summary>
        /// Add an initial value for this property
        /// </summary>
        /// <param name="constVal">the initial value for this property</param>
        public void AddInitValue(Constant constVal)
        {
            this.constVal = constVal;
        }
        public Constant GetInitValue() { return constVal; }
        public void RemoveInitValue() { constVal = null; }


        public Type GetPropertyType() { return returnType; }
        public void SetPropertyType(Type pType) { returnType = pType; }
        public Type[] GetPropertyParams() { return parList; }
        public void SetPropertyParams(Type[] parTypes)
        {
            parList = parTypes;
            if (parList != null) numPars = (uint)parList.Length;
        }


        internal void ChangeRefsToDefs(ClassDef newType, ClassDef[] oldTypes)
        {
            throw new NotYetImplementedException("Merge for Properties");
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.Property, this);
            nameIx = md.AddToStringsHeap(name);
            for (int i = 0; i < numPars; i++)
                parList[i].BuildMDTables(md);
            for (int i = 0; i < tide; i++)
                methods[i].BuildMDTables(md);
            if (constVal != null)
            {
                ConstantElem constElem = new ConstantElem(this, constVal);
                constElem.BuildMDTables(md);
            }
        }

        internal sealed override void BuildSignatures(MetaDataOut md)
        {
            MemoryStream sig = new MemoryStream();
            sig.WriteByte(PropertyTag);
            MetaDataOut.CompressNum(BlobUtil.CompressUInt(numPars), sig);
            returnType.TypeSig(sig);
            for (int i = 0; i < numPars; i++)
            {
                parList[i].BuildSignatures(md);
                parList[i].TypeSig(sig);
            }
            typeBlobIx = md.AddToBlobHeap(sig.ToArray());
            done = false;
        }

        internal override void BuildCILInfo(CILWriter output)
        {
            returnType.BuildCILInfo(output);
            for (int i = 0; i < numPars; i++)
            {
                parList[i].BuildCILInfo(output);
            }
        }

        internal static uint Size(MetaData md)
        {
            return 2 + md.StringsIndexSize() + md.BlobIndexSize();
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write(flags);
            output.StringsIndex(nameIx);
            output.BlobIndex(typeBlobIx);
        }

        internal override void Write(CILWriter output)
        {
            throw new NotYetImplementedException("Write CIL for property");
        }

        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.HasCustomAttr): return 9;
                case (CIx.HasConstant): return 2;
                case (CIx.HasSemantics): return 1;
            }
            return 0;
        }

    }
}