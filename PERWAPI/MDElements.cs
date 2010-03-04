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
    /// Abstract class to represent a row of the Meta Data Tables
    /// </summary>
    public abstract class TableRow
    {
        internal PEReader buffer;
        private uint row = 0;
        /// <summary>
        /// The index of the Meta Data Table containing this element
        /// </summary>
        protected MDTable tabIx;

        /*-------------------- Constructors ---------------------------------*/

        internal TableRow() { }

        internal TableRow(PEReader buff, uint ix, MDTable tableIx)
        {
            buffer = buff;
            row = ix;
            tabIx = tableIx;
        }

        /// <summary>
        /// The row number of this element in the Meta Data Table
        /// </summary>
        public uint Row
        {
            get { return row; }
            set { row = value; }
        }
    }

    /****************************************************/
    /// <summary>
    /// Base class for all Meta Data table elements
    /// </summary>
    public abstract class MetaDataElement : TableRow, IComparable
    {
        /// <summary>
        /// The list of custom attributes associated with this meta data element
        /// </summary>
        protected ArrayList customAttributes;
        protected bool done = false;
        
        protected bool sortTable = false;
        internal bool unresolved = false;

        /*-------------------- Constructors ---------------------------------*/

        internal MetaDataElement() { }

        /// <summary>
        /// Get any custom attributes associated with this meta data element
        /// </summary>
        /// <returns>Array of custom attribute descriptors</returns>
        public CustomAttribute[] GetCustomAttributes()
        {
            if (customAttributes == null) return new CustomAttribute[0];
            return (CustomAttribute[])customAttributes.ToArray(typeof(CustomAttribute));
        }

        /// <summary>
        /// Associate some custom attribute(s) with this meta data element
        /// </summary>
        /// <param name="cas">list of custom attributes</param>
        public void SetCustomAttributes(CustomAttribute[] cas)
        {
            if (cas == null)
                customAttributes = null;
            else
                customAttributes = new ArrayList(cas);
        }

        // FIXME:   this is temporary
        public string GetNameString()
        {
            return this.NameString();
        }

        internal virtual bool isDef() { return false; }

        internal virtual void Resolve(PEReader buff) { }

        internal virtual void ResolveDetails(PEReader buff) { }

        internal virtual uint GetCodedIx(CIx code) { return 0; }

        internal bool NeedToSort() { return sortTable; }

        internal virtual uint SortKey()
        {
            throw new PEFileException("Trying to sort table of " + this);
            //return 0; 
        }

        /// <summary>
        /// Add a custom attribute to this item
        /// </summary>
        /// <param name="ctorMeth">the constructor method for this attribute</param>
        /// <param name="val">the byte value of the parameters</param>
        public void AddCustomAttribute(Method ctorMeth, byte[] val)
        {
            if (customAttributes == null)
            {
                customAttributes = new ArrayList();
            }
            customAttributes.Add(new CustomAttribute(this, ctorMeth, val));
        }

        /// <summary>
        /// Add a custom attribute to this item
        /// </summary>
        /// <param name="ctorMeth">the constructor method for this attribute</param>
        /// <param name="cVals">the constant values of the parameters</param>
        public void AddCustomAttribute(Method ctorMeth, Constant[] cVals)
        {
            if (customAttributes == null)
            {
                customAttributes = new ArrayList();
            }
            customAttributes.Add(new CustomAttribute(this, ctorMeth, cVals));
        }

        /// <summary>
        /// Associate a custom attribute with this meta data element
        /// </summary>
        public void AddCustomAttribute(CustomAttribute ca)
        {
            if (customAttributes == null)
            {
                customAttributes = new ArrayList();
            }
            customAttributes.Add(ca);
        }

        internal uint Token()
        {
            if (Row == 0) throw new Exception("Meta data token is zero!!");
            return (((uint)tabIx << 24) | Row);
        }

        internal void BuildMDTables(MetaDataOut md)
        {
            if (done) return;
            done = true;
            if (Diag.DiagOn) Console.WriteLine("In BuildMDTables");
            BuildTables(md);
            if (customAttributes != null)
            {
                for (int i = 0; i < customAttributes.Count; i++)
                {
                    CustomAttribute ca = (CustomAttribute)customAttributes[i];
                    ca.BuildTables(md);
                }
            }
        }

        internal virtual void BuildTables(MetaDataOut md) { }

        internal virtual void BuildSignatures(MetaDataOut md)
        {
            done = false;
        }

        internal virtual void BuildCILInfo(CILWriter output) { }

        internal virtual void AddToTable(MetaDataOut md)
        {
            md.AddToTable(tabIx, this);
        }

        internal virtual void Write(PEWriter output) { }

        internal virtual void Write(CILWriter output)
        {
            throw new Exception("CIL backend not yet fully implemented - " + GetType().ToString());
        }

        internal virtual string NameString() { return "NoName"; }

        internal void DescriptorError(MetaDataElement elem)
        {
            throw new DescriptorException(elem.NameString());
        }
        #region IComparable Members

        public int CompareTo(object obj)
        {
            uint otherKey = ((MetaDataElement)obj).SortKey();
            uint thisKey = SortKey();
            if (thisKey == otherKey)
            {
                if (this is GenericParam)
                {
                    if (((GenericParam)this).Index < ((GenericParam)obj).Index)
                        return -1;
                    else
                        return 1;
                }
                return 0;
            }
            if (thisKey < otherKey) return -1;
            return 1;
        }

        #endregion
    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for GenericParamConstraint 
    /// </summary>
    public class GenericParamConstraint : MetaDataElement
    {
        uint parentIx, constraintIx;
        GenericParam parent;
        Class constraint;

        /*-------------------- Constructors ---------------------------------*/

        public GenericParamConstraint(GenericParam parent, Class constraint)
        {
            this.parent = parent;
            this.constraint = constraint;
            tabIx = MDTable.GenericParamConstraint;
        }

        internal GenericParamConstraint(PEReader buff)
        {
            parentIx = buff.GetIndex(MDTable.GenericParam);
            constraintIx = buff.GetCodedIndex(CIx.TypeDefOrRef);
            tabIx = MDTable.GenericParamConstraint;
        }

        internal static void Read(PEReader buff, TableRow[] gpars)
        {
            for (int i = 0; i < gpars.Length; i++)
                gpars[i] = new GenericParamConstraint(buff);
        }

        internal override void Resolve(PEReader buff)
        {
            parent = (GenericParam)buff.GetElement(MDTable.GenericParam, parentIx);
            parent.AddConstraint((Class)buff.GetCodedElement(CIx.TypeDefOrRef, constraintIx));
        }

        internal static uint Size(MetaData md)
        {
            return md.TableIndexSize(MDTable.GenericParam) + md.CodedIndexSize(CIx.TypeDefOrRef);
        }

        internal override void Write(PEWriter output)
        {
            output.WriteIndex(MDTable.GenericParam, parent.Row);
            output.WriteCodedIndex(CIx.TypeDefOrRef, constraint);
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for pinvoke information for a method 
    /// </summary>
    public class ImplMap : MetaDataElement
    {
        private static readonly ushort NoMangle = 0x01;
        ushort flags;
        MethodDef meth;
        string importName;
        uint iNameIx, scopeIx = 0, memForIndex = 0;
        ModuleRef importScope;

        /*-------------------- Constructors ---------------------------------*/

        internal ImplMap(ushort flag, MethodDef implMeth, string iName, ModuleRef mScope)
        {
            flags = flag;
            meth = implMeth;
            importName = iName;
            importScope = mScope;
            tabIx = MDTable.ImplMap;
            if (iName == null) flags |= NoMangle;
            sortTable = true;
            //throw(new NotYetImplementedException("PInvoke "));
        }

        internal ImplMap(PEReader buff)
        {
            flags = buff.ReadUInt16();
            memForIndex = buff.GetCodedIndex(CIx.MemberForwarded);
            importName = buff.GetString();
            scopeIx = buff.GetIndex(MDTable.ModuleRef);
            sortTable = true;
            tabIx = MDTable.ImplMap;
        }

        internal static void Read(PEReader buff, TableRow[] impls)
        {
            for (int i = 0; i < impls.Length; i++)
                impls[i] = new ImplMap(buff);
        }

        internal override void Resolve(PEReader buff)
        {
            meth = (MethodDef)buff.GetCodedElement(CIx.MemberForwarded, memForIndex);
            importScope = (ModuleRef)buff.GetElement(MDTable.ModuleRef, scopeIx);
            if (meth != null) meth.AddPInvokeInfo(this);
        }

        internal override uint SortKey()
        {
            return (meth.Row << MetaData.CIxShiftMap[(uint)CIx.MemberForwarded])
                | meth.GetCodedIx(CIx.MemberForwarded);
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.ImplMap, this);
            iNameIx = md.AddToStringsHeap(importName);
            importScope.BuildMDTables(md);
        }

        internal static uint Size(MetaData md)
        {
            return 2 + md.CodedIndexSize(CIx.MemberForwarded) +
                md.StringsIndexSize() + md.TableIndexSize(MDTable.ModuleRef);
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write(flags);
            output.WriteCodedIndex(CIx.MemberForwarded, meth);
            output.StringsIndex(iNameIx);
            output.WriteIndex(MDTable.ModuleRef, importScope.Row);
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Base class for field/methods (member of a class)
    /// </summary>
    public abstract class Member : MetaDataElement
    {
        protected string name;
        protected uint nameIx = 0, sigIx = 0;
        protected byte[] signature;
        protected uint parentIx = 0;
        protected Class parent;

        /*-------------------- Constructors ---------------------------------*/

        internal Member(string memName, Class paren)
        {
            name = memName;
            parent = paren;
            tabIx = MDTable.MemberRef;
        }

        internal Member(uint parenIx, string name, uint sIx)
        {
            parentIx = parenIx;
            this.name = name;
            sigIx = sIx;
            tabIx = MDTable.MemberRef;
        }

        internal Member(string name)
        {
            this.name = name;
            tabIx = MDTable.MemberRef;
        }

        internal static void ReadMember(PEReader buff, TableRow[] members)
        {
            for (int i = 0; i < members.Length; i++)
            {
                uint parenIx = buff.GetCodedIndex(CIx.MemberRefParent);
                string memName = buff.GetString();
                uint sigIx = buff.GetBlobIx();
                if (buff.FirstBlobByte(sigIx) == Field.FieldTag) // got a field
                    members[i] = new FieldRef(parenIx, memName, sigIx);
                else
                    members[i] = new MethodRef(parenIx, memName, sigIx);
            }
        }

        internal virtual Member ResolveParent(PEReader buff) { return null; }

        public MetaDataElement GetParent()
        {
            if (parent == null) return null;
            if (parent.isSpecial())
                return parent.GetParent();
            return parent;
        }

        internal void SetParent(Class paren)
        {
            parent = paren;
        }

        public string Name() { return name; }

        public string QualifiedName() { return parent.TypeName() + "." + name; }

        internal bool HasName(string name)
        {
            return (this.name == name);
        }

        protected void WriteFlags(CILWriter output, uint flags)
        {
            uint vis = (flags & 0x07);  // visibility mask
            switch (vis)
            {
                case 0: output.Write("compilercontrolled "); break;
                case 1: output.Write("private "); break;
                case 2: output.Write("famandassem "); break;
                case 3: output.Write("assembly "); break;
                case 4: output.Write("family "); break;
                case 5: output.Write("famorassem "); break;
                case 6: output.Write("public "); break;
            }
            if ((flags & (ushort)FieldAttr.Static) != 0)
            {
                output.Write("static ");
            }
            if ((flags & (ushort)FieldAttr.Initonly) != 0)
            {
                if (this is MethodDef)
                {
                    output.Write("final ");
                }
                else
                {
                    output.Write("initonly ");
                }
            }
            if ((flags & (ushort)FieldAttr.Literal) != 0)
            {
                if (this is MethodDef)
                {
                    output.Write("virtual ");
                }
                else
                {
                    output.Write("literal ");
                }
            }
            if ((flags & (ushort)FieldAttr.Notserialized) != 0)
            {
                if (this is MethodDef)
                {
                    output.Write("hidebysig ");
                }
                else
                {
                    output.Write("notserialized ");
                }
            }
            if (this is MethodDef)
            {
                // more flags required here
                if ((flags & (ushort)MethAttr.Abstract) != 0)
                {
                    output.Write("abstract ");
                }
                if ((flags & (ushort)MethAttr.SpecialName) != 0)
                {
                    output.Write("specialname ");
                }
                if ((flags & (ushort)MethAttr.RTSpecialName) != 0)
                {
                    output.Write("rtspecialname ");
                }

            }
            else
            {
                // more flags required here
                if ((flags & (ushort)FieldAttr.SpecialName) != 0)
                {
                    output.Write("specialname ");
                }
                if ((flags & (ushort)FieldAttr.RTSpecialName) != 0)
                {
                    output.Write("rtsspecialname ");
                }
            }
        }

        internal abstract void WriteType(CILWriter output);

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for interface implemented by a class
    /// </summary>
    public class InterfaceImpl : MetaDataElement
    {
        ClassDef theClass;
        Class theInterface;
        uint classIx = 0, interfacesIndex = 0;

        /*-------------------- Constructors ---------------------------------*/

        internal InterfaceImpl(ClassDef theClass, Class theInterface)
        {
            this.theClass = theClass;
            this.theInterface = theInterface;
            tabIx = MDTable.InterfaceImpl;
        }

        internal InterfaceImpl(ClassDef theClass, TableRow theInterface)
        {
            this.theClass = theClass;
            this.theInterface = (Class)theInterface;
            tabIx = MDTable.InterfaceImpl;
        }

        internal InterfaceImpl(PEReader buff)
        {
            classIx = buff.GetIndex(MDTable.TypeDef);
            interfacesIndex = buff.GetCodedIndex(CIx.TypeDefOrRef);
            tabIx = MDTable.InterfaceImpl;
        }

        internal override void Resolve(PEReader buff)
        {
            theClass = (ClassDef)buff.GetElement(MDTable.TypeDef, classIx);
            theInterface = (Class)buff.GetCodedElement(CIx.TypeDefOrRef, interfacesIndex);
            theClass.AddImplementedInterface(this);
        }

        internal static void Read(PEReader buff, TableRow[] impls)
        {
            for (int i = 0; i < impls.Length; i++)
                impls[i] = new InterfaceImpl(buff);
        }

        internal ClassDef TheClass() { return theClass; }
        internal Class TheInterface() { return theInterface; }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.InterfaceImpl, this);
            if (!theInterface.isDef()) theInterface.BuildMDTables(md);
            if (theInterface is ClassSpec) md.ConditionalAddTypeSpec(theInterface);
        }

        internal static uint Size(MetaData md)
        {
            return md.TableIndexSize(MDTable.TypeDef) +
                md.CodedIndexSize(CIx.TypeDefOrRef);
        }

        internal sealed override void Write(PEWriter output)
        {
            output.WriteIndex(MDTable.TypeDef, theClass.Row);
            output.WriteCodedIndex(CIx.TypeDefOrRef, theInterface);
        }

        internal sealed override uint GetCodedIx(CIx code) { return 5; }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for resources used in this PE file NOT YET IMPLEMENTED
    /// </summary>
    public class ManifestResource : MetaDataElement
    {
        private static readonly uint PublicResource = 0x1;
        private static readonly uint PrivateResource = 0x2;

        string mrName;
        MetaDataElement impl;  // can be AssemblyRef, ResourceFile or ModuleFile
        uint fileOffset = 0;
        uint nameIx = 0, implIx = 0;
        uint flags = 0;
        PEFile pefile;
        byte[] resourceBytes;

        /*-------------------- Constructors ---------------------------------*/

        internal ManifestResource(PEFile pefile, string name, byte[] resBytes, bool isPub)
        {
            InitResource(pefile, name, isPub);
            this.resourceBytes = resBytes;
        }

        internal ManifestResource(PEFile pefile, string name, MetaDataElement fileRef, uint offset, bool isPub)
        {
            InitResource(pefile, name, isPub);
            impl = fileRef;
            fileOffset = offset;
        }

        internal ManifestResource(PEFile pefile, ManifestResource mres, bool isPub)
        {
            this.pefile = pefile;
            mrName = mres.mrName;
            flags = mres.flags;
            this.impl = mres.impl;
            this.fileOffset = mres.fileOffset;
            this.resourceBytes = mres.resourceBytes;
        }

        internal ManifestResource(PEReader buff)
        {
            fileOffset = buff.ReadUInt32();
            flags = buff.ReadUInt32();
            mrName = buff.GetString();
            implIx = buff.GetCodedIndex(CIx.Implementation);
            tabIx = MDTable.ManifestResource;
        }

        private void InitResource(PEFile pefile, string name, bool isPub)
        {
            this.pefile = pefile;
            mrName = name;
            if (isPub) flags = PublicResource;
            else flags = PrivateResource;
            tabIx = MDTable.ManifestResource;
        }

        internal static void Read(PEReader buff, TableRow[] mrs)
        {
            for (int i = 0; i < mrs.Length; i++)
                mrs[i] = new ManifestResource(buff);
        }

        internal override void Resolve(PEReader buff)
        {
            impl = buff.GetCodedElement(CIx.Implementation, implIx);
            if (impl == null)
            {
                if (!buff.skipBody)
                    resourceBytes = buff.GetResource(fileOffset);
            }
        }

        /*------------------------- public set and get methods --------------------------*/

        public string Name
        {
            get { return mrName; }
            set { mrName = value; }
        }

        public byte[] ResourceBytes
        {
            get { return resourceBytes; }
            set { resourceBytes = value; }
        }

        public AssemblyRef ResourceAssembly
        {
            get { if (impl is AssemblyRef) return (AssemblyRef)impl; return null; }
            set { impl = value; }
        }

        public ResourceFile ResFile
        {
            get { if (impl is ResourceFile) return (ResourceFile)impl; return null; }
            set { impl = value; }
        }

        public ModuleRef ResourceModule
        {
            get { if (impl is ModuleFile) return ((ModuleFile)impl).fileModule; return null; }
            set { impl = value.modFile; }
        }

        public uint FileOffset
        {
            get { return fileOffset; }
            set { fileOffset = value; }
        }

        public bool IsPublic
        {
            get
            {
                return flags == PublicResource;
            }
            set
            {
                if (value)
                    flags = PublicResource;
                else
                    flags = PrivateResource;
            }
        }

        /*----------------------------- internal functions ------------------------------*/

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.ManifestResource, this);
            nameIx = md.AddToStringsHeap(mrName);
            if (resourceBytes != null)
            {
                if (impl != null)
                    throw new Exception("ERROR:  Manifest Resource has byte value and file reference");
                fileOffset = md.AddResource(resourceBytes);
            }
            else
            {
                if (impl == null)
                    throw new Exception("ERROR:  Manifest Resource has no implementation or value");
                impl.BuildMDTables(md);
            }
        }

        internal static uint Size(MetaData md)
        {
            return 8 + md.StringsIndexSize() +
                md.CodedIndexSize(CIx.Implementation);
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write(fileOffset);
            output.Write(flags);
            output.StringsIndex(nameIx);
            output.WriteCodedIndex(CIx.Implementation, impl);
        }

        internal sealed override uint GetCodedIx(CIx code) { return 18; }

    }

    /**************************************************************************/
    /// <summary>
    /// Base class for elements in the PropertyMap, EventMap and 
    /// NestedClass MetaData tables
    /// </summary>
    public class MapElem : MetaDataElement
    {
        ClassDef theClass, parent;
        uint elemIx, classIx, endIx = 0;

        /*-------------------- Constructors ---------------------------------*/

        internal MapElem(ClassDef classDef, uint elIx, MDTable tableIx)
        {
            theClass = classDef;
            elemIx = elIx;
            tabIx = tableIx;
            sortTable = tabIx == MDTable.NestedClass;
        }

        internal MapElem(ClassDef classDef, ClassDef paren, MDTable tableIx)
        {
            theClass = classDef;
            parent = paren;
            tabIx = tableIx;
            sortTable = tabIx == MDTable.NestedClass;
        }

        internal MapElem(PEReader buff, MDTable tab)
        {
            tabIx = tab;
            classIx = buff.GetIndex(MDTable.TypeDef);
            elemIx = buff.GetIndex(tab);
            sortTable = tabIx == MDTable.NestedClass;
        }

        internal static void Read(PEReader buff, TableRow[] maps, MDTable tab)
        {
            if (tab == MDTable.NestedClass)
            {
                for (int i = 0; i < maps.Length; i++)
                {
                    //maps[i] = new MapElem(buff,tab);
                    uint nestClassIx = buff.GetIndex(MDTable.TypeDef);
                    uint enclClassIx = buff.GetIndex(MDTable.TypeDef);
                    ClassDef parent = (ClassDef)buff.GetElement(MDTable.TypeDef, enclClassIx);
                    ClassDef nestClass = ((ClassDef)buff.GetElement(MDTable.TypeDef, nestClassIx)).MakeNestedClass(parent);
                    buff.InsertInTable(MDTable.TypeDef, nestClass.Row, nestClass);
                }
            }
            else
            { // event or property map
                MapElem prev = new MapElem(buff, tab);
                maps[0] = prev;
                for (int i = 1; i < maps.Length; i++)
                {
                    maps[i] = new MapElem(buff, tab);
                    prev.endIx = ((MapElem)maps[i]).elemIx;
                    prev = (MapElem)maps[i];
                }
                switch (tab)
                {
                    case MDTable.PropertyMap:
                        prev.endIx = buff.GetTableSize(MDTable.Property) + 1;
                        break;
                    case MDTable.EventMap:
                        prev.endIx = buff.GetTableSize(MDTable.Event) + 1;
                        break;
                    default:
                        prev.endIx = buff.GetTableSize(tab) + 1;
                        break;
                }
            }
        }

        internal static void ReadNestedClassInfo(PEReader buff, uint num, uint[] parIxs)
        {
            for (int i = 0; i < parIxs.Length; i++) parIxs[i] = 0;
            for (int i = 0; i < num; i++)
            {
                int ix = (int)buff.GetIndex(MDTable.TypeDef);
                parIxs[ix - 1] = buff.GetIndex(MDTable.TypeDef);
            }
        }

        internal override void Resolve(PEReader buff)
        {
            theClass = (ClassDef)buff.GetElement(MDTable.TypeDef, classIx);
            if (tabIx == MDTable.EventMap)
            {
                for (uint i = elemIx; i < endIx; i++)
                    theClass.AddEvent((Event)buff.GetElement(MDTable.Event, i));
            }
            else if (tabIx == MDTable.PropertyMap)
            {
                for (uint i = elemIx; i < endIx; i++)
                    theClass.AddProperty((Property)buff.GetElement(MDTable.Property, i));
            }
            else
            { // must be nested class -- already done
                //ClassDef parent = (ClassDef)buff.GetElement(MDTable.TypeDef,elemIx);
                //parent.MakeNested(theClass);
            }
        }

        internal static uint Size(MetaData md, MDTable tabIx)
        {
            return md.TableIndexSize(MDTable.TypeDef) + md.TableIndexSize(tabIx);
        }

        internal override uint SortKey()
        {
            return theClass.Row;
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(tabIx, this);
        }

        internal sealed override void Write(PEWriter output)
        {
            output.WriteIndex(MDTable.TypeDef, theClass.Row);
            if (parent != null)
                output.WriteIndex(MDTable.TypeDef, parent.Row);
            else
                output.WriteIndex(tabIx, elemIx);
        }
    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for an overriding method (.override)
    /// </summary>
    public class MethodImpl : MetaDataElement
    {
        ClassDef parent;
        Method header, body;
        uint classIx = 0, methBodyIx = 0, methDeclIx = 0;
        bool resolved = true;

        /*-------------------- Constructors ---------------------------------*/

        internal MethodImpl(ClassDef par, Method decl, Method bod)
        {
            parent = par;
            header = decl;
            body = bod;
            tabIx = MDTable.MethodImpl;
        }

        internal MethodImpl(PEReader buff, ClassDef par, uint bIx, uint dIx)
        {
            buffer = buff;
            parent = par;
            methBodyIx = bIx;
            methDeclIx = dIx;
            tabIx = MDTable.MethodImpl;
            resolved = false;
        }

        internal MethodImpl(PEReader buff)
        {
            classIx = buff.GetIndex(MDTable.TypeDef);
            methBodyIx = buff.GetCodedIndex(CIx.MethodDefOrRef);
            methDeclIx = buff.GetCodedIndex(CIx.MethodDefOrRef);
            tabIx = MDTable.MethodImpl;
        }

        /*internal static MethodImpl[] GetMethodImpls(PEReader buff, ClassDef paren, uint classIx) {
          buff.SetElementPosition(MDTable.MethodImpl,0);
          ArrayList impls = new ArrayList();
          for (int i=0; i < buff.GetTableSize(MDTable.MethodImpl); i++) {
            uint cIx = buff.GetIndex(MDTable.TypeDef);
            uint bIx = buff.GetCodedIndex(CIx.MethodDefOrRef);
            uint dIx = buff.GetCodedIndex(CIx.MethodDefOrRef);
            if (cIx == classIx) 
              paren.AddMethodOverride(new MethodImpl(buff,paren,bIx,dIx));
          }
          return (MethodImpl[])impls.ToArray(typeof(MethodImpl));
        }
        */

        public Method Body
        {
            get
            {
                if ((body == null) && (methBodyIx != 0))
                    body = (Method)buffer.GetCodedElement(CIx.MethodDefOrRef, methBodyIx);
                return body;
            }
            set
            {
                body = value;
                if ((!resolved) && (header != null)) resolved = true;
            }
        }

        public Method Header
        {
            get
            {
                if ((header == null) && (methDeclIx != 0))
                    header = (Method)buffer.GetCodedElement(CIx.MethodDefOrRef, methDeclIx);
                return header;
            }
            set
            {
                header = value;
                if ((!resolved) && (body != null)) resolved = true;
            }
        }

        internal void SetOwner(ClassDef cl)
        {
            parent = cl;
        }

        internal static void Read(PEReader buff, TableRow[] impls)
        {
            for (int i = 0; i < impls.Length; i++)
                impls[i] = new MethodImpl(buff);
        }

        internal override void Resolve(PEReader buff)
        {
            body = (Method)buff.GetCodedElement(CIx.MethodDefOrRef, methBodyIx);
            header = (Method)buff.GetCodedElement(CIx.MethodDefOrRef, methDeclIx);
            parent = (ClassDef)buff.GetElement(MDTable.TypeDef, classIx);
            parent.AddMethodImpl(this);
            resolved = true;
        }

        internal void ResolveMethDetails()
        {
            body = (Method)buffer.GetCodedElement(CIx.MethodDefOrRef, methBodyIx);
            header = (Method)buffer.GetCodedElement(CIx.MethodDefOrRef, methDeclIx);
            resolved = true;
        }

        internal void ChangeRefsToDefs(ClassDef newType, ClassDef[] oldTypes)
        {
            throw new NotYetImplementedException("Merge for MethodImpls");
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.MethodImpl, this);
            if (!resolved) ResolveMethDetails();
            if (body is MethodRef) body.BuildMDTables(md);
            if (header is MethodRef) header.BuildMDTables(md);
        }

        internal static uint Size(MetaData md)
        {
            return md.TableIndexSize(MDTable.TypeDef) + 2 * md.CodedIndexSize(CIx.MethodDefOrRef);
        }

        internal sealed override void Write(PEWriter output)
        {
            output.WriteIndex(MDTable.TypeDef, parent.Row);
            output.WriteCodedIndex(CIx.MethodDefOrRef, body);
            output.WriteCodedIndex(CIx.MethodDefOrRef, header);
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for Property and Event methods
    /// </summary>
    public class MethodSemantics : MetaDataElement
    {
        MethodType type;
        MethodDef meth;
        Feature eventOrProp;
        uint methIx = 0, assocIx = 0;

        /*-------------------- Constructors ---------------------------------*/

        internal MethodSemantics(MethodType mType, MethodDef method, Feature feature)
        {
            type = mType;
            meth = method;
            eventOrProp = feature;
            sortTable = true;
            tabIx = MDTable.MethodSemantics;
        }

        internal MethodSemantics(PEReader buff)
        {
            type = (MethodType)buff.ReadUInt16();
            methIx = buff.GetIndex(MDTable.Method);
            assocIx = buff.GetCodedIndex(CIx.HasSemantics);
            sortTable = true;
            tabIx = MDTable.MethodSemantics;
        }

        internal static void Read(PEReader buff, TableRow[] methSems)
        {
            for (int i = 0; i < methSems.Length; i++)
                methSems[i] = new MethodSemantics(buff);
        }

        internal override void Resolve(PEReader buff)
        {
            meth = (MethodDef)buff.GetElement(MDTable.Method, methIx);
            eventOrProp = (Feature)buff.GetCodedElement(CIx.HasSemantics, assocIx);
            eventOrProp.AddMethod(this);
        }

        internal MethodType GetMethodType() { return type; }

        internal MethodDef GetMethod() { return meth; }

        internal override uint SortKey()
        {
            return meth.Row;
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.MethodSemantics, this);
        }

        internal static uint Size(MetaData md)
        {
            return 2 + md.TableIndexSize(MDTable.Method) + md.CodedIndexSize(CIx.HasSemantics);
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write((ushort)type);
            output.WriteIndex(MDTable.Method, meth.Row);
            output.WriteCodedIndex(CIx.HasSemantics, eventOrProp);
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a parameter of a method defined in this assembly/module
    /// </summary>
    public class Param : MetaDataElement
    {
        private static readonly ushort hasDefault = 0x1000;
        private static readonly ushort noDefault = 0xEFFF;
        private static readonly ushort hasFieldMarshal = 0x2000;
        private static readonly ushort noFieldMarshal = 0xDFFF;

        protected string pName;
        protected uint nameIx = 0;
        Type pType;
        internal ushort seqNo = 0;
        ushort parMode;
        Constant defaultVal;
        NativeType marshalType;

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create a new parameter for a method
        /// </summary>
        /// <param name="mode">param mode (in, out, opt)</param>
        /// <param name="parName">parameter name</param>
        /// <param name="parType">parameter type</param>
        public Param(ParamAttr mode, string parName, Type parType)
        {
            pName = parName;
            pType = parType;
            parMode = (ushort)mode;
            tabIx = MDTable.Param;
        }

        // EXPERIMENTAL kjg Nov 19 2007
        public static Param DefaultParam()
        {
            return new Param(ParamAttr.Default, "", null);
        }

        internal Param(PEReader buff)
        {
            parMode = buff.ReadUInt16();
            seqNo = buff.ReadUInt16();
            pName = buff.GetString();
            tabIx = MDTable.Param;
        }

        internal static void Read(PEReader buff, TableRow[] pars)
        {
            for (int i = 0; i < pars.Length; i++)
                pars[i] = new Param(buff);
        }

        internal void Resolve(PEReader buff, uint fIx, Type type)
        {
            this.pType = type;
        }

        /// <summary>
        /// Add a default value to this parameter
        /// </summary>
        /// <param name="cVal">the default value for the parameter</param>
        public void AddDefaultValue(Constant cVal)
        {
            defaultVal = cVal;
            parMode |= hasDefault;
        }

        /// <summary>
        /// Get the default constant value for this parameter
        /// </summary>
        /// <returns></returns>
        public Constant GetDefaultValue() { return defaultVal; }

        /// <summary>
        /// Remove the default constant value for this parameter
        /// </summary>
        public void RemoveDefaultValue() { defaultVal = null; parMode &= noDefault; }

        /// <summary>
        /// Add marshalling information about this parameter
        /// </summary>
        public void SetMarshalType(NativeType mType)
        {
            marshalType = mType;
            parMode |= hasFieldMarshal;
        }
        /// <summary>
        /// Get the parameter marshalling information
        /// </summary>
        /// <returns>The native type to marshall to</returns>
        public NativeType GetMarshalType() { return marshalType; }

        /// <summary>
        /// Remove any marshalling information for this parameter
        /// </summary>
        public void RemoveMashalType() { marshalType = null; parMode &= noFieldMarshal; }

        /// <summary>
        /// Get the type of this parameter
        /// </summary>
        public Type GetParType() { return pType; }

        /// <summary>
        /// Set the type of this parameter
        /// </summary>
        public void SetParType(Type parType) { pType = parType; }

        public void AddAttribute(ParamAttr att)
        {
            this.parMode |= (ushort)att;
        }

        public ParamAttr GetAttributes() { return (ParamAttr)parMode; }

        public void SetAttributes(ParamAttr att)
        {
            this.parMode = (ushort)att;
        }

        /// <summary>
        /// Retrieve the name of this parameter
        /// </summary>
        /// <returns>parameter name</returns>
        public string GetName() { return pName; }

        /// <summary>
        /// Set the name of this parameter
        /// </summary>
        /// <param name="nam">parameter name</param>
        public void SetName(string nam) { pName = nam; }

        /*------------------------ internal functions ----------------------------*/

        internal Param Copy(Type paramType)
        {
            return new Param((ParamAttr)parMode, pName, paramType);
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.Param, this);
            nameIx = md.AddToStringsHeap(pName);
            if (defaultVal != null)
            {
                ConstantElem constElem = new ConstantElem(this, defaultVal);
                constElem.BuildMDTables(md);
            }
            if (marshalType != null)
            {
                FieldMarshal marshalInfo = new FieldMarshal(this, marshalType);
                marshalInfo.BuildMDTables(md);
            }
        }

        internal override void BuildCILInfo(CILWriter output)
        {
            pType.BuildCILInfo(output);
        }

        internal void TypeSig(MemoryStream str)
        {
            pType.TypeSig(str);
        }

        internal static uint Size(MetaData md)
        {
            return 4 + md.StringsIndexSize();
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write(parMode);
            output.Write(seqNo);
            output.StringsIndex(nameIx);
        }

        internal override void Write(CILWriter output)
        {
            pType.WriteType(output);
            output.Write(" " + pName);
        }


        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.HasCustomAttr): return 4;
                case (CIx.HasConstant): return 1;
                case (CIx.HasFieldMarshal): return 1;
            }
            return 0;
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Summary description for ConstantElem.
    /// </summary>
    internal class ConstantElem : MetaDataElement
    {
        MetaDataElement parent;
        Constant cValue;
        uint valIx = 0, parentIx = 0;

        /*-------------------- Constructors ---------------------------------*/

        internal ConstantElem(MetaDataElement parent, Constant val)
        {
            this.parent = parent;
            cValue = val;
            sortTable = true;
            tabIx = MDTable.Constant;
        }

        internal ConstantElem(PEReader buff)
        {
            byte constType = buff.ReadByte();
            byte pad = buff.ReadByte();
            parentIx = buff.GetCodedIndex(CIx.HasConstant);
            //valIx = buff.GetBlobIx();
            cValue = buff.GetBlobConst(constType);
            sortTable = true;
            tabIx = MDTable.Constant;
        }

        internal override void Resolve(PEReader buff)
        {
            parent = buff.GetCodedElement(CIx.HasConstant, parentIx);
            if (parent != null)
            {
                if (parent is Param) ((Param)parent).AddDefaultValue(cValue);
                else if (parent is FieldDef) ((FieldDef)parent).AddValue(cValue);
                else ((Property)parent).AddInitValue(cValue);
            }
        }

        internal static void Read(PEReader buff, TableRow[] consts)
        {
            for (int i = 0; i < consts.Length; i++)
                consts[i] = new ConstantElem(buff);
        }

        /*----------------------------- internal functions ------------------------------*/

        internal override uint SortKey()
        {
            return (parent.Row << MetaData.CIxShiftMap[(uint)CIx.HasConstant])
                | parent.GetCodedIx(CIx.HasConstant);
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.Constant, this);
            valIx = cValue.GetBlobIndex(md);
        }

        internal static uint Size(MetaData md)
        {
            return 2 + md.CodedIndexSize(CIx.HasConstant) + md.BlobIndexSize();
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write(cValue.GetTypeIndex());
            output.Write((byte)0);
            output.WriteCodedIndex(CIx.HasConstant, parent);
            output.BlobIndex(valIx);
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for security permissions for a class or a method NOT YET IMPLEMENTED
    /// </summary>
    public class DeclSecurity : MetaDataElement
    {
        SecurityAction action;
        MetaDataElement parent;
        uint parentIx = 0, permissionIx;
        byte[] permissionSet;

        /*-------------------- Constructors ---------------------------------*/

        internal DeclSecurity(MetaDataElement paren, SecurityAction act, byte[] perSet)
        {
            parent = paren;
            action = act;
            permissionSet = perSet;
            sortTable = true;
            tabIx = MDTable.DeclSecurity;
        }

        internal DeclSecurity(PEReader buff)
        {
            action = (SecurityAction)buff.ReadUInt16();
            parentIx = buff.GetCodedIndex(CIx.HasDeclSecurity);
            permissionSet = buff.GetBlob();
            sortTable = true;
            tabIx = MDTable.DeclSecurity;
        }

        internal static void Read(PEReader buff, TableRow[] secs)
        {
            for (int i = 0; i < secs.Length; i++)
                secs[i] = new DeclSecurity(buff);
        }

        internal static DeclSecurity FindSecurity(PEReader buff, MetaDataElement paren, uint codedParIx)
        {
            buff.SetElementPosition(MDTable.DeclSecurity, 0);
            for (int i = 0; i < buff.GetTableSize(MDTable.DeclSecurity); i++)
            {
                uint act = buff.ReadUInt16();
                if (buff.GetCodedIndex(CIx.HasDeclSecurity) == codedParIx)
                    return new DeclSecurity(paren, (SecurityAction)act, buff.GetBlob());
                uint junk = buff.GetBlobIx();
            }
            return null;
        }

        internal override void Resolve(PEReader buff)
        {
            parent = buff.GetCodedElement(CIx.HasDeclSecurity, parentIx);
            if (parent != null)
            {
                if (parent is ClassDef) ((ClassDef)parent).AddSecurity(this);
                if (parent is Assembly) ((Assembly)parent).AddSecurity(this);
                if (parent is MethodDef) ((MethodDef)parent).AddSecurity(this);
            }
        }

        internal override uint SortKey()
        {
            return (parent.Row << MetaData.CIxShiftMap[(uint)CIx.HasDeclSecurity])
                | parent.GetCodedIx(CIx.HasDeclSecurity);
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.DeclSecurity, this);
            permissionIx = md.AddToBlobHeap(permissionSet);
        }

        internal static uint Size(MetaData md)
        {
            return 2 + md.CodedIndexSize(CIx.HasDeclSecurity) + md.BlobIndexSize();
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write((UInt16)action);   // or should this be 2 bytes??
            output.WriteCodedIndex(CIx.HasDeclSecurity, parent);
            output.BlobIndex(permissionIx);
        }

    }
}