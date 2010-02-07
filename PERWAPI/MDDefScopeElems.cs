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
    /// A scope for definitions
    /// </summary>
    public abstract class DefiningScope : ResolutionScope
    {

        /*-------------------- Constructors ---------------------------------*/

        internal DefiningScope(string name)
            : base(name)
        {
            readAsDef = true;
        }

        internal override void AddToClassList(Class aClass)
        {
            ((ClassDef)aClass).SetScope((PEFile)this);
            classes.Add(aClass);
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for an assembly (.assembly)
    /// </summary>
    public class Assembly : DefiningScope
    {
        //internal static Hashtable Assemblies = new Hashtable();
        ushort majorVer, minorVer, buildNo, revisionNo;
        uint flags;
        HashAlgorithmType hashAlgId = HashAlgorithmType.None;
        uint keyIx = 0, cultIx = 0;
        byte[] publicKey;
        string culture;
        internal AssemblyRef refOf;
        ArrayList security;
        internal PEFile pefile;

        /*-------------------- Constructors ---------------------------------*/

        internal Assembly(string name, PEFile pefile)
            : base(name)
        {
            this.pefile = pefile;
            tabIx = MDTable.Assembly;
        }

        internal Assembly(string name, HashAlgorithmType hashAlgId, ushort majVer,
            ushort minVer, ushort bldNo, ushort revNo, uint flags, byte[] pKey,
            string cult, PEFile pefile)
            : base(name)
        {
            this.hashAlgId = hashAlgId;
            this.majorVer = majVer;
            this.minorVer = minVer;
            this.buildNo = bldNo;
            this.revisionNo = revNo;
            this.flags = flags;
            this.publicKey = pKey;
            this.culture = cult;
            tabIx = MDTable.Assembly;
        }

        internal static AssemblyRef ReadAssemblyRef(PEReader buff)
        {
            buff.SetElementPosition(MDTable.Assembly, 1);
            HashAlgorithmType hAlg = (HashAlgorithmType)buff.ReadUInt32();
            ushort majVer = buff.ReadUInt16();
            ushort minVer = buff.ReadUInt16();
            ushort bldNo = buff.ReadUInt16();
            ushort revNo = buff.ReadUInt16();
            uint flags = buff.ReadUInt32();
            byte[] pKey = buff.GetBlob();
            string name = buff.GetString();
            string cult = buff.GetString();
            AssemblyRef assemRef = null;
            if (name.ToLower() == "mscorlib")
            {
                assemRef = MSCorLib.mscorlib;
                assemRef.AddVersionInfo(majVer, minVer, bldNo, revNo);
                if (pKey.Length > 8) assemRef.AddKey(pKey);
                else assemRef.AddKeyToken(pKey);
                assemRef.AddCulture(cult);
                assemRef.SetFlags(flags);
            }
            else
            {
                assemRef = new AssemblyRef(name, majVer, minVer, bldNo, revNo, flags, pKey, cult, null);
            }
            //AssemblyRef assemRef = new AssemblyRef(name,majVer,minVer,bldNo,revNo,flags,pKey,cult,null);
            assemRef.ReadAsDef();
            return assemRef;
        }

        internal static void Read(PEReader buff, TableRow[] table, PEFile pefile)
        {
            for (int i = 0; i < table.Length; i++)
            {
                HashAlgorithmType hAlg = (HashAlgorithmType)buff.ReadUInt32();
                ushort majVer = buff.ReadUInt16();
                ushort minVer = buff.ReadUInt16();
                ushort bldNo = buff.ReadUInt16();
                ushort revNo = buff.ReadUInt16();
                uint flags = buff.ReadUInt32();
                byte[] pKey = buff.GetBlob();
                string name = buff.GetString();
                string cult = buff.GetString();
                table[i] = new Assembly(name, hAlg, majVer, minVer, bldNo, revNo, flags, pKey, cult, pefile);
            }
        }

        /*------------------------- public set and get methods --------------------------*/

        /// <summary>
        /// Add details about an assembly
        /// </summary>
        /// <param name="majVer">Major Version</param>
        /// <param name="minVer">Minor Version</param>
        /// <param name="bldNo">Build Number</param>
        /// <param name="revNo">Revision Number</param>
        /// <param name="key">Hash Key</param>
        /// <param name="hash">Hash Algorithm</param>
        /// <param name="cult">Culture</param>
        public void AddAssemblyInfo(int majVer, int minVer, int bldNo, int revNo,
            byte[] key, HashAlgorithmType hash, string cult)
        {
            majorVer = (ushort)majVer;
            minorVer = (ushort)minVer;
            buildNo = (ushort)bldNo;
            revisionNo = (ushort)revNo;
            hashAlgId = hash;
            publicKey = key;
            culture = cult;
        }

        /// <summary>
        /// Get the major version number for this Assembly
        /// </summary>
        /// <returns>major version number</returns>
        public int MajorVersion() { return majorVer; }
        /// <summary>
        /// Get the minor version number for this Assembly
        /// </summary>
        /// <returns>minor version number</returns>
        public int MinorVersion() { return minorVer; }
        /// <summary>
        /// Get the build number for this Assembly
        /// </summary>
        /// <returns>build number</returns>
        public int BuildNumber() { return buildNo; }
        /// <summary>
        /// Get the revision number for this Assembly
        /// </summary>
        /// <returns>revision number</returns>
        public int RevisionNumber() { return revisionNo; }
        /// <summary>
        /// Get the public key for this Assembly
        /// </summary>
        /// <returns>public key bytes</returns>
        public byte[] Key() { return publicKey; }
        /// <summary>
        /// Get the public key token for this assembly
        /// or null if the assembly is not signed
        /// </summary>
        /// <returns>Key token or null</returns>
        public byte[] KeyTokenBytes()
        {
            if (this.publicKey != null &&
                this.hashAlgId == HashAlgorithmType.SHA1)
            {
                int ix, ofst;
                byte[] token = new byte[8];
                HashAlgorithm sha = new SHA1CryptoServiceProvider();
                byte[] hash = sha.ComputeHash(publicKey);
                for (ix = 0, ofst = hash.Length - 8; ix < 8; ix++)
                    token[ix] = hash[ix + ofst];
                return token;
            }
            else
                return null;
        }
        /// <summary>
        /// Returns Public Key Token as Int64
        /// </summary>
        /// <returns></returns>
        public long KeyTokenAsLong()
        {
            byte[] token = KeyTokenBytes();
            return (token == null ? 0 : BitConverter.ToInt64(token, 0));
        }
        /// <summary>
        /// Get the type of the hash algorithm for this Assembly
        /// </summary>
        /// <returns>hash algorithm type</returns>
        public HashAlgorithmType HashAlgorithm() { return hashAlgId; }
        /// <summary>
        /// Get the culture information for this Assembly
        /// </summary>
        /// <returns>culture string</returns>
        public string Culture() { return culture; }

        /// <summary>
        /// Add some security action(s) to this Assembly
        /// </summary>
        public void AddSecurity(SecurityAction act, byte[] permissionSet)
        {
            AddSecurity(new DeclSecurity(this, act, permissionSet));
            // securityActions = permissionSet;
        }

        /// <summary>
        /// Get the security information for this assembly
        /// </summary>
        /// <returns>security information</returns>
        public DeclSecurity[] GetSecurity()
        {
            if (security == null) return null;
            return (DeclSecurity[])security.ToArray(typeof(DeclSecurity));
        }

        /// <summary>
        /// Check if this assembly has security information
        /// </summary>
        public bool HasSecurity() { return security != null; }

        /// <summary>
        /// Set the attributes for this assembly
        /// </summary>
        /// <param name="aa">assembly attribute</param>     
        public void SetAssemblyAttr(AssemAttr aa)
        {
            flags = (uint)aa;
        }

        /// <summary>
        /// Add an attribute for this assembly
        /// </summary>
        /// <param name="aa">assembly attribute</param>     
        public void AddAssemblyAttr(AssemAttr aa)
        {
            flags |= (uint)aa;
        }

        /// <summary>
        /// Get the attributes of this assembly
        /// </summary>
        /// <returns>assembly attributes</returns>
        public AssemAttr AssemblyAttributes()
        {
            return (AssemAttr)flags;
        }

        /// <summary>
        /// Make an AssemblyRef descriptor for this Assembly
        /// </summary>
        /// <returns>AssemblyRef descriptor for this Assembly</returns>
        public AssemblyRef MakeRefOf()
        {
            if (refOf == null)
            {
                refOf = new AssemblyRef(name, majorVer, minorVer, buildNo, revisionNo,
                    flags, publicKey, culture, null);
            }
            return refOf;
        }

        /*------------------------ internal functions ----------------------------*/

        internal void AddSecurity(DeclSecurity sec)
        {
            if (security == null) security = new ArrayList();
            security.Add(sec);
        }

        internal static uint Size(MetaData md)
        {
            return 16 + md.BlobIndexSize() + 2 * md.StringsIndexSize();
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.Assembly, this);
            nameIx = md.AddToStringsHeap(name);
            cultIx = md.AddToStringsHeap(culture);
            keyIx = md.AddToBlobHeap(publicKey);
            if (security != null)
            {
                for (int i = 0; i < security.Count; i++)
                {
                    ((DeclSecurity)security[i]).BuildMDTables(md);
                }
            }
        }

        internal sealed override void Write(PEWriter output)
        {
            //Console.WriteLine("Writing assembly element with nameIx of " + nameIx + " at file offset " + output.Seek(0,SeekOrigin.Current));
            output.Write((uint)hashAlgId);
            output.Write(majorVer);
            output.Write(minorVer);
            output.Write(buildNo);
            output.Write(revisionNo);
            output.Write(flags);
            output.BlobIndex(keyIx);
            output.StringsIndex(nameIx);
            output.StringsIndex(cultIx);
        }

        internal override void Write(CILWriter output)
        {
            output.WriteLine(".assembly " + name + " { }");
        }


        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.HasCustomAttr): return 14;
                case (CIx.HasDeclSecurity): return 2;
            }
            return 0;
        }


    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a module
    /// </summary>
    public abstract class Module : DefiningScope
    {
        Guid mvid;
        uint mvidIx = 0;
        internal ModuleRef refOf;
        /// <summary>
        /// The default class "Module" for globals
        /// </summary>
        protected ClassDef defaultClass;
        /// <summary>
        /// Is this module a .dll or .exe
        /// </summary>
        //protected bool isDLL;
        /// <summary>
        /// Is this module mscorlib.dll
        /// </summary>
        protected bool ismscorlib = false;
        /// <summary>
        /// Managed resources for this module
        /// </summary>
        protected ArrayList resources = new ArrayList();

        /*-------------------- Constructors ---------------------------------*/

        internal Module(string mName)
            : base(GetBaseName(mName))
        {
            mvid = Guid.NewGuid();
            //isDLL = name.EndsWith(".dll") || name.EndsWith(".DLL");
            defaultClass = new ClassDef((PEFile)this, TypeAttr.Private, "", "<Module>");
            defaultClass.MakeSpecial();
            tabIx = MDTable.Module;
            ismscorlib = name.ToLower() == "mscorlib.dll";
            if (Diag.DiagOn) Console.WriteLine("Module name = " + name);
        }

        internal void Read(PEReader buff)
        {
            buff.ReadZeros(2);
            name = buff.GetString();
            mvid = buff.GetGUID();
            uint junk = buff.GetGUIDIx();
            junk = buff.GetGUIDIx();
            if (Diag.DiagOn) Console.WriteLine("Reading module with name " + name + " and Mvid = " + mvid);
            ismscorlib = name.ToLower() == "mscorlib.dll";
        }

        internal static ModuleRef ReadModuleRef(PEReader buff)
        {
            buff.ReadZeros(2);
            string name = buff.GetString();
            uint junk = buff.GetGUIDIx();
            junk = buff.GetGUIDIx();
            junk = buff.GetGUIDIx();
            ModuleRef mRef = new ModuleRef(new ModuleFile(name, null));
            mRef.ReadAsDef();
            return mRef;
        }

        /*------------------------- public set and get methods --------------------------*/

        /// <summary>
        /// Add a class to this Module
        /// If this class already exists, throw an exception
        /// </summary>
        /// <param name="attrSet">attributes of this class</param>
        /// <param name="nsName">name space name</param>
        /// <param name="name">class name</param>
        /// <returns>a descriptor for this new class</returns>
        public ClassDef AddClass(TypeAttr attrSet, string nsName, string name)
        {
            ClassDef aClass = GetClass(nsName, name);
            if (aClass != null)
                throw new DescriptorException("Class " + aClass.NameString());
            aClass = new ClassDef((PEFile)this, attrSet, nsName, name);
            classes.Add(aClass);
            return aClass;
        }

        /// <summary>
        /// Add a class which extends System.ValueType to this Module
        /// If this class already exists, throw an exception
        /// </summary>
        /// <param name="attrSet">attributes of this class</param>
        /// <param name="nsName">name space name</param>
        /// <param name="name">class name</param>
        /// <returns>a descriptor for this new class</returns>
        public ClassDef AddValueClass(TypeAttr attrSet, string nsName, string name)
        {
            ClassDef aClass = AddClass(attrSet, nsName, name);
            aClass.SuperType = MSCorLib.mscorlib.ValueType();
            aClass.MakeValueClass();
            return aClass;
        }

        /// <summary>
        /// Add a class to this PE File
        /// </summary>
        /// <param name="attrSet">attributes of this class</param>
        /// <param name="nsName">name space name</param>
        /// <param name="name">class name</param>
        /// <param name="superType">super type of this class (extends)</param>
        /// <returns>a descriptor for this new class</returns>
        public ClassDef AddClass(TypeAttr attrSet, string nsName, string name, Class superType)
        {
            ClassDef aClass = AddClass(attrSet, nsName, name);
            aClass.SuperType = superType;
            return aClass;
        }

        /// <summary>
        /// Add a class to this module
        /// If this class already exists, throw an exception
        /// </summary>
        /// <param name="aClass">The class to be added</param>
        public void AddClass(ClassDef aClass)
        {
            ClassDef eClass = GetClass(aClass.NameSpace(), aClass.Name());
            if (eClass != null)
                throw new DescriptorException("Class " + aClass.NameString());
            classes.Add(aClass);
            // MERGE change Refs to Defs here, fix this
            aClass.SetScope((PEFile)this);
        }

        /// <summary>
        /// Get a class of this module, if no class exists, return null
        /// </summary>
        /// <param name="name">The name of the class to get</param>
        /// <returns>ClassDef for name or null</returns>
        public ClassDef GetClass(string name)
        {
            return (ClassDef)GetClass(null, name, false);
        }

        /// <summary>
        /// Get a class of this module, if no class exists, return null
        /// </summary>
        /// <param name="nsName">The namespace of the class</param>
        /// <param name="name">The name of the class to get</param>
        /// <returns>ClassDef for nsName.name or null</returns>
        public ClassDef GetClass(string nsName, string name)
        {
            return (ClassDef)GetClass(nsName, name, true);
        }

        /// <summary>
        /// Get all the classes of this module
        /// </summary>
        /// <returns>An array containing a ClassDef for each class of this module</returns>
        public ClassDef[] GetClasses()
        {
            return (ClassDef[])classes.ToArray(typeof(ClassDef));
        }

        /// <summary>
        /// Add a "global" method to this module
        /// </summary>
        /// <param name="name">method name</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">method parameters</param>
        /// <returns>a descriptor for this new "global" method</returns>
        public MethodDef AddMethod(string name, Type retType, Param[] pars)
        {
            MethodDef newMeth = defaultClass.AddMethod(name, retType, pars);
            return newMeth;
        }

        /// <summary>
        /// Add a "global" method to this module
        /// </summary>
        /// <param name="name">method name</param>
        /// <param name="genPars">generic parameters</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">method parameters</param>
        /// <returns>a descriptor for this new "global" method</returns>
        public MethodDef AddMethod(string name, GenericParam[] genPars, Type retType, Param[] pars)
        {
            MethodDef newMeth = defaultClass.AddMethod(name, genPars, retType, pars);
            return newMeth;
        }

        /// <summary>
        /// Add a "global" method to this module
        /// </summary>
        /// <param name="mAtts">method attributes</param>
        /// <param name="iAtts">method implementation attributes</param>
        /// <param name="name">method name</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">method parameters</param>
        /// <returns>a descriptor for this new "global" method</returns>
        public MethodDef AddMethod(MethAttr mAtts, ImplAttr iAtts, string name, Type retType, Param[] pars)
        {
            MethodDef newMeth = defaultClass.AddMethod(mAtts, iAtts, name, retType, pars);
            return newMeth;
        }

        /// <summary>
        /// Add a "global" method to this module
        /// </summary>
        /// <param name="mAtts">method attributes</param>
        /// <param name="iAtts">method implementation attributes</param>
        /// <param name="name">method name</param>
        /// <param name="genPars">generic parameters</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">method parameters</param>
        /// <returns>a descriptor for this new "global" method</returns>
        public MethodDef AddMethod(MethAttr mAtts, ImplAttr iAtts, string name, GenericParam[] genPars, Type retType, Param[] pars)
        {
            MethodDef newMeth = defaultClass.AddMethod(mAtts, iAtts, name, genPars, retType, pars);
            return newMeth;
        }

        /// <summary>
        /// Add a "global" method to this module
        /// </summary>
        /// <param name="meth">The method to be added</param>
        public void AddMethod(MethodDef meth)
        {
            defaultClass.AddMethod(meth);
        }

        /// <summary>
        /// Get a method of this module, if it exists
        /// </summary>
        /// <param name="name">The name of the method to get</param>
        /// <returns>MethodDef for name, or null if one does not exist</returns>
        public MethodDef GetMethod(string name)
        {
            return defaultClass.GetMethod(name);
        }

        /// <summary>
        /// Get all the methods of this module with a specified name
        /// </summary>
        /// <param name="name">The name of the method(s)</param>
        /// <returns>An array of all the methods of this module called "name" </returns>
        public MethodDef[] GetMethods(string name)
        {
            return defaultClass.GetMethods(name);
        }

        /// <summary>
        /// Get a method of this module, if it exists
        /// </summary>
        /// <param name="name">The name of the method to get</param>
        /// <param name="parTypes">The signature of the method</param>
        /// <returns>MethodDef for name(parTypes), or null if one does not exist</returns>
        public MethodDef GetMethod(string name, Type[] parTypes)
        {
            return defaultClass.GetMethod(name, parTypes);
        }

        /// <summary>
        /// Get all the methods of this module
        /// </summary>
        /// <returns>An array of all the methods of this module</returns>
        public MethodDef[] GetMethods()
        {
            return defaultClass.GetMethods();
        }

        /// <summary>
        /// Delete a method from this module
        /// </summary>
        /// <param name="meth">The method to be deleted</param>
        public void RemoveMethod(MethodDef meth)
        {
            defaultClass.RemoveMethod(meth);
        }

        /// <summary>
        /// Delete a method from this module
        /// </summary>
        /// <param name="name">The name of the method to be deleted</param>
        public void RemoveMethod(string name)
        {
            defaultClass.RemoveMethod(name);
        }

        /// <summary>
        /// Delete a method from this module
        /// </summary>
        /// <param name="name">The name of the method to be deleted</param>
        /// <param name="parTypes">The signature of the method to be deleted</param>
        public void RemoveMethod(string name, Type[] parTypes)
        {
            defaultClass.RemoveMethod(name, parTypes);
        }

        /// <summary>
        /// Delete a method from this module
        /// </summary>
        /// <param name="ix">The index of the method (in the method array
        /// returned by GetMethods()) to be deleted</param>
        public void RemoveMethod(int ix)
        {
            defaultClass.RemoveMethod(ix);
        }

        /// <summary>
        /// Add a "global" field to this module
        /// </summary>
        /// <param name="name">field name</param>
        /// <param name="fType">field type</param>
        /// <returns>a descriptor for this new "global" field</returns>
        public FieldDef AddField(string name, Type fType)
        {
            FieldDef newField = defaultClass.AddField(name, fType);
            return newField;
        }

        /// <summary>
        /// Add a "global" field to this module
        /// </summary>
        /// <param name="attrSet">attributes of this field</param>
        /// <param name="name">field name</param>
        /// <param name="fType">field type</param>
        /// <returns>a descriptor for this new "global" field</returns>
        public FieldDef AddField(FieldAttr attrSet, string name, Type fType)
        {
            FieldDef newField = defaultClass.AddField(attrSet, name, fType);
            return newField;
        }

        /// <summary>
        /// Add a "global" field to this module
        /// </summary>
        /// <param name="fld">The field to be added</param>
        public void AddField(FieldDef fld)
        {
            defaultClass.AddField(fld);
        }

        /// <summary>
        /// Get a field of this module, if it exists
        /// </summary>
        /// <param name="name">The name of the field</param>
        /// <returns>FieldDef for "name", or null if one doesn't exist</returns>
        public FieldDef GetField(string name)
        {
            return defaultClass.GetField(name);
        }

        /// <summary>
        /// Get all the fields of this module
        /// </summary>
        /// <returns>An array of all the fields of this module</returns>
        public FieldDef[] GetFields()
        {
            return defaultClass.GetFields();
        }

        /// <summary>
        /// Make a ModuleRef for this Module.
        /// </summary>
        /// <returns>ModuleRef for this Module</returns>
        public ModuleRef MakeRefOf(/*bool hasEntryPoint, byte[] hashValue*/)
        {
            if (refOf == null)
            {
                refOf = new ModuleRef(name/*,hasEntryPoint,hashValue*/);
                refOf.defOf = this;
            }/* else {  // fix this
        if (hasEntryPoint)
          refOf.SetEntryPoint();
        refOf.SetHash(hashValue);
      }*/
            return refOf;
        }

        /// <summary>
        /// Set the name for this module
        /// </summary>
        /// <param name="newName">New module name</param>
        public void SetName(string newName)
        {
            name = newName;
            //isDLL = name.EndsWith(".dll") || name.EndsWith(".DLL");
        }

        public void SetMVid(Guid guid)
        {
            mvid = guid;
        }

        public Guid GetMVid()
        {
            return mvid;
        }

        /*------------------------- internal functions --------------------------*/

        internal bool isMSCorLib() { return ismscorlib; }

        internal bool isDefaultClass(ClassDef aClass) { return aClass == defaultClass; }

        private static string GetBaseName(string name)
        {
            // more to this??
            if (name.IndexOf("\\") != -1)
                name = name.Substring(name.LastIndexOf("\\") + 1);
            return name;
        }

        internal void SetDefaultClass(ClassDef dClass)
        {
            defaultClass = dClass;
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.Module, this);
            nameIx = md.AddToStringsHeap(name);
            mvidIx = md.AddToGUIDHeap(mvid);
            defaultClass.BuildTables(md);
            for (int i = 0; i < classes.Count; i++)
            {
                ((Class)classes[i]).BuildMDTables(md);
            }
            for (int i = 0; i < resources.Count; i++)
            {
                ((ManifestResource)resources[i]).BuildMDTables(md);
            }
        }


        internal static uint Size(MetaData md)
        {
            return 2 + md.StringsIndexSize() + 3 * md.GUIDIndexSize();
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write((short)0);
            output.StringsIndex(nameIx);
            output.GUIDIndex(mvidIx);
            output.GUIDIndex(0);
            output.GUIDIndex(0);
        }

        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.HasCustomAttr): return 7;
                case (CIx.ResolutionScope): return 0;
            }
            return 0;
        }
    }
}