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
    /// Base class for scopes (extended by Module, ModuleRef, Assembly, AssemblyRef)
    /// </summary>
    public abstract class ResolutionScope : MetaDataElement
    {
        internal protected uint nameIx = 0;
        internal protected string name;
        internal protected ArrayList classes = new ArrayList();
        internal protected bool readAsDef = false;

        /*-------------------- Constructors ---------------------------------*/

        internal ResolutionScope(string name)
        {
            this.name = name;
        }

        internal virtual void AddToClassList(Class aClass)
        {
            classes.Add(aClass);
        }

        internal Class GetExistingClass(string nameSpace, string name)
        {
            for (int i = 0; i < classes.Count; i++)
            {
                Class aClass = (Class)classes[i];
                if ((aClass.Name() == name) && (aClass.NameSpace() == nameSpace))
                    return aClass;
            }
            return null;
        }

        protected Class GetClass(string nameSpace, string name, bool both)
        {
            for (int i = 0; i < classes.Count; i++)
            {
                Object aClass = classes[i];
                if ((((Class)aClass).Name() == name) &&
                    (!both || (both && (((Class)aClass).NameSpace() == nameSpace))))
                    return (Class)aClass;
            }
            return null;
        }

        /// <summary>
        /// Delete a class from this module
        /// </summary>
        /// <param name="aClass">The name of the class to be deleted</param>
        public void RemoveClass(Class aClass)
        {
            classes.Remove(aClass);
        }

        /// <summary>
        /// Delete the class at an index in the class array
        /// </summary>
        /// <param name="ix">The index of the class to be deleted (from 0)</param>
        public void RemoveClass(int ix)
        {
            classes.RemoveAt(ix);
        }

        public string Name() { return name; }

        internal override string NameString() { return "[" + name + "]"; }

    }

    /**************************************************************************/
    /// <summary>
    /// A scope for descriptors which are referenced
    /// </summary>
    public abstract class ReferenceScope : ResolutionScope
    {
        /// <summary>
        /// A default class decriptor for globals
        /// </summary>
        protected ClassRef defaultClass;

        /*-------------------- Constructors ---------------------------------*/

        internal ReferenceScope(string name)
            : base(name)
        {
            defaultClass = new ClassRef(this, "", "");
            defaultClass.MakeSpecial();
        }

        internal void ReadAsDef()
        {
            readAsDef = true;
        }

        internal ClassRef GetDefaultClass() { return defaultClass; }

        internal void SetDefaultClass(ClassRef dClass)
        {
            defaultClass = dClass;
        }

        internal override void AddToClassList(Class aClass)
        {
            ((ClassRef)aClass).SetScope(this);
            classes.Add(aClass);
        }

        internal void ReplaceClass(Class aClass)
        {
            bool found = false;
            for (int i = 0; (i < classes.Count) && !found; i++)
            {
                if (((Class)classes[i]).Name() == aClass.Name())
                {
                    found = true;
                }
            }
            if (!found)
                classes.Add(aClass);
        }

        internal bool isDefaultClass(ClassRef aClass) { return aClass == defaultClass; }

        /// <summary>
        /// Add a class to this Scope.  If this class already exists, throw
        /// an exception
        /// </summary>
        /// <param name="newClass">The class to be added</param>
        public void AddClass(ClassRef newClass)
        {
            ClassRef aClass = (ClassRef)GetClass(newClass.NameSpace(), newClass.Name(), true);
            if (aClass != null)
                throw new DescriptorException("Class " + newClass.NameString());
            if (Diag.DiagOn) Console.WriteLine("Adding class " + newClass.Name() + " to ResolutionScope " + name);
            classes.Add(newClass);
            // Change Refs to Defs here
            newClass.SetScope(this);
        }

        /// <summary>
        /// Add a class to this Scope.  If the class already exists,
        /// throw an exception.  
        /// </summary>
        /// <param name="nsName">name space name</param>
        /// <param name="name">class name</param>
        /// <returns>a descriptor for this class in another module</returns>
        public virtual ClassRef AddClass(string nsName, string name)
        {
            ClassRef aClass = GetClass(nsName, name);
            if (aClass != null)
            {
                if ((aClass is SystemClass) && (!((SystemClass)aClass).added))
                    ((SystemClass)aClass).added = true;
                else
                    throw new DescriptorException("Class " + aClass.NameString());
            }
            else
            {
                aClass = new ClassRef(this, nsName, name);
                classes.Add(aClass);
            }
            return aClass;
        }

        /// <summary>
        /// Add a value class to this scope.  If the class already exists,
        /// throw an exception.  
        /// </summary>
        /// <param name="nsName">name space name</param>
        /// <param name="name">class name</param>
        /// <returns></returns>
        public virtual ClassRef AddValueClass(string nsName, string name)
        {
            ClassRef aClass = AddClass(nsName, name);
            aClass.MakeValueClass();
            return aClass;
        }

        /// <summary>
        /// Get a class of this scope, if it exists.
        /// </summary>
        /// <param name="name">The name of the class.</param>
        /// <returns>ClassRef for "name".</returns>
        public ClassRef GetClass(string name)
        {
            return (ClassRef)GetClass(null, name, false);
        }

        /// <summary>
        /// Get a class of this scope, if it exists.
        /// </summary>
        /// <param name="nsName">The namespace of the class.</param>
        /// <param name="name">The name of the class.</param>
        /// <returns>ClassRef for "nsName.name".</returns>
        public ClassRef GetClass(string nsName, string name)
        {
            return (ClassRef)GetClass(nsName, name, true);
        }

        /// <summary>
        /// Get all the classes in this scope.
        /// </summary>
        /// <returns>An array of all the classes in this scope.</returns>
        public ClassRef[] GetClasses()
        {
            return (ClassRef[])classes.ToArray(typeof(ClassRef));
        }

        /// <summary>
        /// Fetch a MethodRef descriptor for the method "retType name (pars)".
        /// If one exists, it is returned, else one is created.
        /// </summary>
        /// <param name="name">method name</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">method parameter types</param>
        /// <returns>a descriptor for this method in anther module</returns>
        public MethodRef AddMethod(string name, Type retType, Type[] pars)
        {
            MethodRef meth = defaultClass.AddMethod(name, retType, pars);
            return meth;
        }

        /// <summary>
        /// Fetch a MethodRef descriptor for the method "retType name (pars, optPars)".
        /// If one exists, it is returned, else one is created.
        /// </summary>
        /// <param name="name">method name</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">parameter types</param>
        /// <param name="optPars">optional param types for this vararg method</param>
        /// <returns>a descriptor for this method</returns>
        public MethodRef AddVarArgMethod(string name, Type retType, Type[] pars, Type[] optPars)
        {
            MethodRef meth = defaultClass.AddVarArgMethod(name, retType, pars, optPars);
            return meth;
        }

        /// <summary>
        /// Add a method to this scope.
        /// </summary>
        /// <param name="meth">The method to be added.</param>
        public void AddMethod(MethodRef meth)
        {
            defaultClass.AddMethod(meth);
        }

        //		internal void CheckAddMethod(MethodRef meth) {
        //			defaultClass.CheckAddMethod(meth);
        //		}
        /*
            internal void CheckAddMethods(ArrayList meths) {
              for (int i=0; i < meths.Count; i++) {
                Method meth = (Method)meths[i];
                defaultClass.CheckAddMethod(meth);
                meth.SetParent(this);
              }
            }

            internal MethodRef GetMethod(string name, uint sigIx) {
              return defaultClass.GetMethod(name,sigIx);
            }
            */

        /// <summary>
        /// Get a method of this scope, if it exists.
        /// </summary>
        /// <param name="name">The name of the method.</param>
        /// <returns>MethodRef for "name", or null if none exists.</returns>
        public MethodRef GetMethod(string name)
        {
            return defaultClass.GetMethod(name);
        }

        /// <summary>
        /// Get all the methods with a specified name in this scope.
        /// </summary>
        /// <param name="name">The name of the method(s).</param>
        /// <returns>An array of all the methods called "name".</returns>
        public MethodRef[] GetMethods(string name)
        {
            return defaultClass.GetMethods(name);
        }


        /// <summary>
        /// Get a method of this scope, if it exists.
        /// </summary>
        /// <param name="name">The name of the method</param>
        /// <param name="parTypes">The signature of the method.</param>
        /// <returns>MethodRef for name(parTypes).</returns>
        public MethodRef GetMethod(string name, Type[] parTypes)
        {
            return defaultClass.GetMethod(name, parTypes);
        }

        /// <summary>
        /// Get a vararg method of this scope, if it exists.
        /// </summary>
        /// <param name="name">The name of the method.</param>
        /// <param name="parTypes">The signature of the method.</param>
        /// <param name="optPars">The optional parameters of the vararg method.</param>
        /// <returns>MethodRef for name(parTypes,optPars).</returns>
        public MethodRef GetMethod(string name, Type[] parTypes, Type[] optPars)
        {
            return defaultClass.GetMethod(name, parTypes, optPars);
        }

        /// <summary>
        /// Get all the methods in this module
        /// </summary>
        /// <returns>Array of the methods of this module</returns>
        public MethodRef[] GetMethods()
        {
            return defaultClass.GetMethods();
        }

        /// <summary>
        /// Delete a method from this scope.
        /// </summary>
        /// <param name="meth">The method to be deleted.</param>
        public void RemoveMethod(MethodRef meth)
        {
            defaultClass.RemoveMethod(meth);
        }

        /// <summary>
        /// Delete a method from this scope.  If there are multiple methods with
        /// the same name, the first on the list will be deleted.
        /// </summary>
        /// <param name="name">The name of the method to delete.</param>
        public void RemoveMethod(string name)
        {
            defaultClass.RemoveMethod(name);
        }

        /// <summary>
        /// Delete a method from this scope.
        /// </summary>
        /// <param name="name">The name of the method to be deleted.</param>
        /// <param name="parTypes">The signature of the method to be deleted.</param>
        public void RemoveMethod(string name, Type[] parTypes)
        {
            defaultClass.RemoveMethod(name, parTypes);
        }

        /// <summary>
        /// Delete a (vararg) method from this scope.
        /// </summary>
        /// <param name="name">The name of the method to be deleted.</param>
        /// <param name="parTypes">The signature of the method to be deleted.</param>
        /// <param name="optTypes">The optional parameters of the vararg method.</param>
        public void RemoveMethod(string name, Type[] parTypes, Type[] optTypes)
        {
            defaultClass.RemoveMethod(name, parTypes, optTypes);
        }

        /// <summary>
        /// Delete a method from this scope.
        /// </summary>
        /// <param name="index">The index of the method to be deleted.  Index
        /// into array returned by GetMethods().</param>
        public void RemoveMethod(int index)
        {
            defaultClass.RemoveMethod(index);
        }

        /// <summary>
        /// Add a field to this scope.
        /// </summary>
        /// <param name="name">field name</param>
        /// <param name="fType">field type</param>
        /// <returns>a descriptor for the field "name" in this scope</returns>
        public FieldRef AddField(string name, Type fType)
        {
            FieldRef field = defaultClass.AddField(name, fType);
            return field;
        }

        /// <summary>
        /// Add a field to this scope.
        /// </summary>
        /// <param name="fld">The field to be added</param>
        public void AddField(FieldRef fld)
        {
            defaultClass.AddField(fld);
        }

        /// <summary>
        /// Add a number of fields to this scope.
        /// </summary>
        /// <param name="flds">The fields to be added.</param>
        internal void AddFields(ArrayList flds)
        {
            for (int i = 0; i < flds.Count; i++)
            {
                FieldRef fld = (FieldRef)flds[i];
                defaultClass.AddField(fld);
            }
        }

        /// <summary>
        /// Fetch the FieldRef descriptor for the field "name" in this module, 
        /// if one exists
        /// </summary>
        /// <param name="name">field name</param>
        /// <returns>FieldRef descriptor for "name" or null</returns>
        public FieldRef GetField(string name)
        {
            return defaultClass.GetField(name);
        }

        /// <summary>
        /// Get all the fields of this module
        /// </summary>
        /// <returns>Array of FieldRefs for this module</returns>
        public FieldRef[] GetFields()
        {
            return defaultClass.GetFields();
        }

        internal void AddToMethodList(MethodRef meth)
        {
            defaultClass.AddToMethodList(meth);
        }

        internal void AddToFieldList(FieldRef fld)
        {
            defaultClass.AddToFieldList(fld);
        }

        internal MethodRef GetMethod(MethSig mSig)
        {
            return (MethodRef)defaultClass.GetMethod(mSig);
        }


    }

    /**************************************************************************/
    /// <summary>
    /// A reference to an external assembly (.assembly extern)
    /// </summary>
    public class AssemblyRef : ReferenceScope
    {
        private ushort major, minor, build, revision;
        uint flags, keyIx, hashIx, cultIx;
        bool hasVersion = false, isKeyToken = false;
        byte[] keyBytes, hashBytes;
        string culture;

        /*-------------------- Constructors ---------------------------------*/

        internal AssemblyRef(string name)
            : base(name)
        {
            tabIx = MDTable.AssemblyRef;
        }

        internal AssemblyRef(string name, ushort maj, ushort min, ushort bldNo, ushort rev,
            uint flags, byte[] kBytes, string cult, byte[] hBytes)
            : base(name)
        {
            tabIx = MDTable.AssemblyRef;
            major = maj;
            minor = min;
            build = bldNo;
            revision = rev;
            this.flags = flags;  // check
            keyBytes = kBytes;  // need to set is token or full key
            if (keyBytes != null)
                isKeyToken = keyBytes.Length <= 8;
            culture = cult;
            hashBytes = hBytes;
            tabIx = MDTable.AssemblyRef;
        }

        internal static AssemblyRef Read(PEReader buff)
        {
            ushort majVer = buff.ReadUInt16();
            ushort minVer = buff.ReadUInt16();
            ushort bldNo = buff.ReadUInt16();
            ushort revNo = buff.ReadUInt16();
            uint flags = buff.ReadUInt32();
            byte[] pKey = buff.GetBlob();
            string name = buff.GetString();
            string cult = buff.GetString();
            byte[] hBytes = buff.GetBlob();
            AssemblyRef assemRef;
            if (name.ToLower() == "mscorlib")
            {
                assemRef = MSCorLib.mscorlib;
                assemRef.AddVersionInfo(majVer, minVer, bldNo, revNo);
                assemRef.AddHash(hBytes);
                if (pKey.Length > 8) assemRef.AddKey(pKey);
                else assemRef.AddKeyToken(pKey);
                assemRef.AddCulture(cult);
                assemRef.SetFlags(flags);
            }
            else
            {
                assemRef = new AssemblyRef(name, majVer, minVer, bldNo, revNo, flags, pKey, cult, hBytes);
            }
            return assemRef;
        }

        internal static void Read(PEReader buff, TableRow[] table)
        {
            for (int i = 0; i < table.Length; i++)
                table[i] = Read(buff);
        }

        /*------------------------- public set and get methods --------------------------*/

        /// <summary>
        /// Add version information about this external assembly
        /// </summary>
        /// <param name="majVer">Major Version</param>
        /// <param name="minVer">Minor Version</param>
        /// <param name="bldNo">Build Number</param>
        /// <param name="revNo">Revision Number</param>
        public void AddVersionInfo(int majVer, int minVer, int bldNo, int revNo)
        {
            major = (ushort)majVer;
            minor = (ushort)minVer;
            build = (ushort)bldNo;
            revision = (ushort)revNo;
            hasVersion = true;
        }

        /// <summary>
        /// Get the major version for this external assembly
        /// </summary>
        /// <returns>major version number</returns>
        public int MajorVersion() { return major; }
        /// <summary>
        /// Get the minor version for this external assembly
        /// </summary>
        /// <returns>minor version number</returns>
        public int MinorVersion() { return minor; }
        /// <summary>
        /// Get the build number for this external assembly
        /// </summary>
        /// <returns>build number</returns>
        public int BuildNumber() { return build; }
        /// <summary>
        /// Get the revision number for this external assembly
        /// </summary>
        /// <returns>revision number</returns>
        public int RevisionNumber() { return revision; }

        /// <summary>
        /// Check if this external assembly has any version information
        /// </summary>
        public bool HasVersionInfo() { return hasVersion; }

        /// <summary>
        /// Add the hash value for this external assembly
        /// </summary>
        /// <param name="hash">bytes of the hash value</param>
        public void AddHash(byte[] hash) { hashBytes = hash; }

        /// <summary>
        /// Get the hash value for this external assembly
        /// </summary>
        /// <returns></returns>
        public byte[] GetHash() { return hashBytes; }

        /// <summary>
        /// Set the culture for this external assembly
        /// </summary>
        /// <param name="cult">the culture string</param>
        public void AddCulture(string cult) { culture = cult; }

        public string GetCulture() { return culture; }

        /// <summary>
        /// Add the full public key for this external assembly
        /// </summary>
        /// <param name="key">bytes of the public key</param>
        public void AddKey(byte[] key)
        {
            flags |= 0x0001;   // full public key
            keyBytes = key;
        }

        /// <summary>
        /// Add the public key token (low 8 bytes of the public key)
        /// </summary>
        /// <param name="key">low 8 bytes of public key</param>
        public void AddKeyToken(byte[] key)
        {
            keyBytes = key;
            isKeyToken = true;
        }

        /// <summary>
        /// Get the public key token
        /// </summary>
        /// <returns>bytes of public key</returns>
        public byte[] GetKey() { return keyBytes; }

        /// <summary>
        /// Make an AssemblyRef for "name".  
        /// </summary>
        /// <param name="name">The name of the assembly</param>
        /// <returns>AssemblyRef for "name".</returns>
        public static AssemblyRef MakeAssemblyRef(string name)
        {
            AssemblyRef assemRef = new AssemblyRef(name);
            return assemRef;
        }

        public static AssemblyRef MakeAssemblyRef(
            string name,
            int majVer,
            int minVer,
            int bldNum,
            int revNum,
            byte[] key)
        {
            AssemblyRef assemRef = new AssemblyRef(name);
            assemRef.AddVersionInfo(majVer, minVer, bldNum, revNum);
            if (key.Length > 8)
                assemRef.AddKey(key);
            else
                assemRef.AddKeyToken(key);
            return assemRef;
        }

        /*------------------------ internal functions ----------------------------*/

        internal void SetFlags(uint flags)
        {
            this.flags = flags;
        }

        internal string AssemblyString()
        {
            string result = name;
            if (hasVersion)
                result = result + ", Version=" + major + "." + minor + "." +
                    build + "." + revision;
            if (keyBytes != null)
            {
                string tokenStr = "=";
                if (isKeyToken) tokenStr = "Token=";
                result = result + ", PublicKey" + tokenStr;
                for (int i = 0; i < keyBytes.Length; i++)
                {
                    result = result + Hex.Byte(keyBytes[i]);
                }
            }
            if (culture != null)
                result = result + ", Culture=" + culture;
            return result;
        }

        internal static uint Size(MetaData md)
        {
            return 12 + 2 * md.StringsIndexSize() + 2 * md.BlobIndexSize();
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.AssemblyRef, this);
            keyIx = md.AddToBlobHeap(keyBytes);
            nameIx = md.AddToStringsHeap(name);
            cultIx = md.AddToStringsHeap(culture);
            hashIx = md.AddToBlobHeap(hashBytes);
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write(major);
            output.Write(minor);
            output.Write(build);
            output.Write(revision);
            output.Write(flags);
            output.BlobIndex(keyIx);
            output.StringsIndex(nameIx);
            output.StringsIndex(cultIx);
            output.BlobIndex(hashIx);
        }

        internal override void Write(CILWriter output)
        {
            output.WriteLine(".assembly extern " + name + " { }");
        }


        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.ResolutionScope): return 2;
                case (CIx.HasCustomAttr): return 15;
                case (CIx.Implementation): return 1;
            }
            return 0;
        }

    }
    /**************************************************************************/
    /// <summary>
    /// The assembly for mscorlib.  
    /// </summary>
    public sealed class MSCorLib : AssemblyRef
    {
        internal static MSCorLib mscorlib = new MSCorLib();
        internal SystemClass ObjectClass;
        private ClassRef valueType;

        internal MSCorLib()
            : base("mscorlib")
        {
            classes.Add(new SystemClass(this, PrimitiveType.Void));
            classes.Add(new SystemClass(this, PrimitiveType.Boolean));
            classes.Add(new SystemClass(this, PrimitiveType.Char));
            classes.Add(new SystemClass(this, PrimitiveType.Int8));
            classes.Add(new SystemClass(this, PrimitiveType.UInt8));
            classes.Add(new SystemClass(this, PrimitiveType.Int16));
            classes.Add(new SystemClass(this, PrimitiveType.UInt16));
            classes.Add(new SystemClass(this, PrimitiveType.Int32));
            classes.Add(new SystemClass(this, PrimitiveType.UInt32));
            classes.Add(new SystemClass(this, PrimitiveType.Int64));
            classes.Add(new SystemClass(this, PrimitiveType.UInt64));
            classes.Add(new SystemClass(this, PrimitiveType.Float32));
            classes.Add(new SystemClass(this, PrimitiveType.Float64));
            classes.Add(new SystemClass(this, PrimitiveType.IntPtr));
            classes.Add(new SystemClass(this, PrimitiveType.UIntPtr));
            classes.Add(new SystemClass(this, PrimitiveType.String));
            classes.Add(new SystemClass(this, PrimitiveType.TypedRef));
            ObjectClass = new SystemClass(this, PrimitiveType.Object);
            classes.Add(ObjectClass);
            valueType = new ClassRef(this, "System", "ValueType");
            valueType.MakeValueClass();
            classes.Add(valueType);
        }

        internal ClassRef ValueType()
        {
            return valueType;
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a module in an assembly
    /// </summary>
    public class ModuleRef : ReferenceScope
    {
        ArrayList exportedClasses = new ArrayList();
        internal ModuleFile modFile;
        internal Module defOf;
        internal bool ismscorlib = false;

        /*-------------------- Constructors ---------------------------------*/

        internal ModuleRef(string name, bool entryPoint, byte[] hashValue)
            : base(name)
        {
            modFile = new ModuleFile(name, hashValue, entryPoint);
            ismscorlib = name.ToLower() == "mscorlib.dll";
            tabIx = MDTable.ModuleRef;
        }

        internal ModuleRef(string name)
            : base(name)
        {
            ismscorlib = name.ToLower() == "mscorlib.dll";
            tabIx = MDTable.ModuleRef;
        }

        internal ModuleRef(ModuleFile file)
            : base(file.Name())
        {
            modFile = file;
            tabIx = MDTable.ModuleRef;
        }

        internal static void Read(PEReader buff, TableRow[] mods, bool resolve)
        {
            for (int i = 0; i < mods.Length; i++)
            {
                string name = buff.GetString();
                ModuleRef mRef = new ModuleRef(name);
                if (resolve) mRef.modFile = buff.GetFileDesc(name);
                mods[i] = mRef;
            }
        }

        internal sealed override void Resolve(PEReader buff)
        {
            modFile = buff.GetFileDesc(name);
            if (modFile != null)
                modFile.fileModule = this;
        }

        /*------------------------- public set and get methods --------------------------*/


        /// <summary>
        /// Add a class which is declared public in this external module of
        /// THIS assembly.  This class will be exported from this assembly.
        /// The ilasm syntax for this is .extern class
        /// </summary>
        /// <param name="attrSet">attributes of the class to be exported</param>
        /// <param name="nsName">name space name</param>
        /// <param name="name">external class name</param>
        /// <param name="declFile">the file where the class is declared</param>
        /// <param name="isValueClass">is this class a value type?</param>
        /// <returns>a descriptor for this external class</returns>
        public ClassRef AddExternClass(TypeAttr attrSet, string nsName,
            string name, bool isValueClass, PEFile pefile)
        {
            ClassRef cRef = new ClassRef(this, nsName, name);
            if (isValueClass) cRef.MakeValueClass();
            ExternClass eClass = new ExternClass(attrSet, nsName, name, modFile);
            exportedClasses.Add(eClass);
            cRef.SetExternClass(eClass);
            classes.Add(cRef);
            return cRef;
        }

        public static ModuleRef MakeModuleRef(string name, bool entryPoint, byte[] hashValue)
        {
            ModuleRef mRef = new ModuleRef(name, entryPoint, hashValue);
            return mRef;
        }

        public void SetEntryPoint()
        {
            modFile.SetEntryPoint();
        }

        public void SetHash(byte[] hashVal)
        {
            modFile.SetHash(hashVal);
        }

        /*------------------------- internal functions --------------------------*/

        /*    internal void AddMember(Member memb) {
              if (memb is Method) {
                Method existing = GetMethod(memb.Name(),((Method)memb).GetParTypes());
                if (existing == null) 
                  methods.Add(memb);
              } else {
                Field existing = GetField(memb.Name());
                if (existing == null)
                  fields.Add(memb);
              }
            }
            */

        internal void AddToExportedClassList(ClassRef exClass)
        {
            if (exportedClasses.Contains(exClass)) return;
            exportedClasses.Add(exClass);
        }

        internal void AddExternClass(ExternClass eClass)
        {
            exportedClasses.Add(eClass);
        }

        internal static uint Size(MetaData md)
        {
            return md.StringsIndexSize();
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.ModuleRef, this);
            nameIx = md.AddToStringsHeap(name);
            if (modFile != null) modFile.BuildMDTables(md);
            for (int i = 0; i < exportedClasses.Count; i++)
                ((ExternClass)exportedClasses[i]).BuildMDTables(md);
        }

        internal sealed override void Write(PEWriter output)
        {
            output.StringsIndex(nameIx);
        }

        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.HasCustomAttr): return 12;
                case (CIx.MemberRefParent): return 2;
                case (CIx.ResolutionScope): return 1;
            }
            return 0;
        }

    }
}