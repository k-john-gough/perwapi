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
    /// Descriptor for a class/interface declared in another module of THIS 
    /// assembly, or in another assembly.
    /// </summary>
    public class ClassRef : ClassDesc
    {
        protected ReferenceScope scope;
        protected uint resScopeIx = 0;
        internal ExternClass externClass;
        internal ClassDef defOf;
        internal bool readAsDef = false;

        /*-------------------- Constructors ---------------------------------*/

        internal ClassRef(ReferenceScope scope, string nsName, string name)
            : base(nsName, name)
        {
            this.scope = scope;
            tabIx = MDTable.TypeRef;
        }

        internal ClassRef(uint scopeIx, string nsName, string name)
            : base(nsName, name)
        {
            resScopeIx = scopeIx;
            tabIx = MDTable.TypeRef;
        }

        internal static ClassRef ReadDef(PEReader buff, ReferenceScope resScope, uint index)
        {
            uint junk = buff.ReadUInt32();
            string cName = buff.GetString();
            string nsName = buff.GetString();
            ClassRef newClass = (ClassRef)resScope.GetExistingClass(nsName, cName);
            if (newClass == null)
            {
                newClass = new ClassRef(resScope, nsName, cName);
                resScope.AddToClassList(newClass);
            }
            newClass.readAsDef = true;
            newClass.Row = index;
            junk = buff.GetCodedIndex(CIx.TypeDefOrRef);
            newClass.fieldIx = buff.GetIndex(MDTable.Field);
            newClass.methodIx = buff.GetIndex(MDTable.Method);
            return newClass;
        }

        internal static void Read(PEReader buff, TableRow[] typeRefs, bool resolve)
        {
            for (uint i = 0; i < typeRefs.Length; i++)
            {
                uint resScopeIx = buff.GetCodedIndex(CIx.ResolutionScope);
                string name = buff.GetString();
                string nameSpace = buff.GetString();
                if (buff.CodedTable(CIx.ResolutionScope, resScopeIx) == MDTable.TypeRef)
                    typeRefs[i] = new NestedClassRef(resScopeIx, nameSpace, name);
                else
                    typeRefs[i] = new ClassRef(resScopeIx, nameSpace, name);
                typeRefs[i].Row = i + 1;
            }
            if (resolve)
            {
                for (int i = 0; i < typeRefs.Length; i++)
                {
                    ((ClassRef)typeRefs[i]).ResolveParent(buff, false);
                }
            }
        }

        internal static ClassRef ReadClass(PEReader buff, ReferenceScope resScope)
        {
            uint resScopeIx = buff.GetCodedIndex(CIx.ResolutionScope);
            string name = buff.GetString();
            string nameSpace = buff.GetString();
            ClassRef newClass = (ClassRef)resScope.GetExistingClass(nameSpace, name);
            if (newClass == null)
                newClass = new ClassRef(resScope, nameSpace, name);
            return newClass;
        }

        internal virtual void ResolveParent(PEReader buff, bool isExtern)
        {
            CIx cIx = CIx.ResolutionScope;
            if (isExtern) cIx = CIx.Implementation;
            if (scope != null) return;
            MetaDataElement parentScope = buff.GetCodedElement(cIx, resScopeIx);
            if (parentScope is Module)
            {  // special code for glitch in Everett ilasm
                ClassDef newDef = new ClassDef((PEFile)parentScope, 0, nameSpace, name);
                ((Module)parentScope).AddToClassList(newDef);
                buff.InsertInTable(MDTable.TypeRef, Row, newDef);
            }
            else
            {
                scope = (ReferenceScope)buff.GetCodedElement(cIx, resScopeIx);
                ClassRef existing = (ClassRef)scope.GetExistingClass(nameSpace, name);
                if (existing == null)
                {
                    scope.AddToClassList(this);
                }
                else
                {
                    if (isExtern)
                        buff.InsertInTable(MDTable.ExportedType, Row, existing);
                    else
                        buff.InsertInTable(MDTable.TypeRef, Row, existing);
                }
            }
        }

        /*------------------------- public set and get methods --------------------------*/

        /// <summary>
        /// Add a method to this class
        /// </summary>
        /// <param name="name">method name</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">parameter types</param>
        /// <returns>a descriptor for this method</returns>
        public MethodRef AddMethod(string name, Type retType, Type[] pars)
        {
            System.Diagnostics.Debug.Assert(retType != null);
            MethodRef meth = (MethodRef)GetMethodDesc(name, pars);
            if (meth != null) DescriptorError(meth);
            meth = new MethodRef(this, name, retType, pars);
            methods.Add(meth);
            return meth;
        }

        /// <summary>
        /// Add a method to this class
        /// </summary>
        /// <param name="name">method name</param>
        /// <param name="genPars">generic parameters</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">parameter types</param>
        /// <returns>a descriptor for this method</returns>
        public MethodRef AddMethod(string name, GenericParam[] genPars, Type retType, Type[] pars)
        {
            MethodRef meth = AddMethod(name, retType, pars);
            if ((genPars != null) && (genPars.Length > 0))
            {
                for (int i = 0; i < genPars.Length; i++)
                {
                    genPars[i].SetMethParam(meth, i);
                }
                meth.SetGenericParams(genPars);
            }
            return meth;
        }

        /// <summary>
        /// Add a method to this class
        /// </summary>
        /// <param name="name">method name</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">parameter types</param>
        /// <param name="optPars">optional parameter types</param>
        /// <returns>a descriptor for this method</returns>
        public MethodRef AddVarArgMethod(string name, Type retType, Type[] pars, Type[] optPars)
        {
            MethodRef meth = AddMethod(name, retType, pars);
            meth.MakeVarArgMethod(null, optPars);
            return meth;
        }
        /// <summary>
        /// Add a method to this class
        /// </summary>
        /// <param name="name">method name</param>
        /// <param name="genPars">generic parameters</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">parameter types</param>
        /// <param name="optPars">optional parameter types</param>
        /// <returns>a descriptor for this method</returns>
        public MethodRef AddVarArgMethod(string name, GenericParam[] genPars, Type retType, Type[] pars, Type[] optPars)
        {
            MethodRef meth = AddMethod(name, genPars, retType, pars);
            meth.MakeVarArgMethod(null, optPars);
            return meth;
        }

        /// <summary>
        /// Get the method "name" for this class
        /// </summary>
        /// <param name="name">The method name</param>
        /// <returns>Descriptor for the method "name" for this class</returns>
        public MethodRef GetMethod(string name)
        {
            return (MethodRef)GetMethodDesc(name);
        }

        /// <summary>
        /// Get the method "name(parTypes)" for this class
        /// </summary>
        /// <param name="name">Method name</param>
        /// <param name="parTypes">Method signature</param>
        /// <returns>Descriptor for "name(parTypes)"</returns>
        public MethodRef GetMethod(string name, Type[] parTypes)
        {
            return (MethodRef)GetMethodDesc(name, parTypes);
        }

        /// <summary>
        /// Get the vararg method "name(parTypes,optTypes)" for this class
        /// </summary>
        /// <param name="name">Method name</param>
        /// <param name="parTypes">Method parameter types</param>
        /// <param name="optTypes">Optional parameter types</param>
        /// <returns>Descriptor for "name(parTypes,optTypes)"</returns>
        public MethodRef GetMethod(string name, Type[] parTypes, Type[] optTypes)
        {
            return (MethodRef)GetMethodDesc(name, parTypes, optTypes);
        }

        /// <summary>
        /// Get the descriptors for the all methods name "name" for this class
        /// </summary>
        /// <param name="name">Method name</param>
        /// <returns>List of methods called "name"</returns>
        public MethodRef[] GetMethods(string name)
        {
            ArrayList meths = GetMeths(name);
            return (MethodRef[])meths.ToArray(typeof(MethodRef));
        }


        /// <summary>
        /// Get all the methods for this class
        /// </summary>
        /// <returns>List of methods for this class</returns>
        public MethodRef[] GetMethods()
        {
            return (MethodRef[])methods.ToArray(typeof(MethodRef));
        }

        /// <summary>
        /// Add a field to this class
        /// </summary>
        /// <param name="name">field name</param>
        /// <param name="fType">field type</param>
        /// <returns>a descriptor for this field</returns>
        public FieldRef AddField(string name, Type fType)
        {
            FieldRef fld = (FieldRef)FindField(name);
            if (fld != null) DescriptorError(fld);
            fld = new FieldRef(this, name, fType);
            fields.Add(fld);
            return fld;
        }

        /// <summary>
        /// Get the descriptor for the field "name" for this class
        /// </summary>
        /// <param name="name">Field name</param>
        /// <returns>Descriptor for field "name"</returns>
        public FieldRef GetField(string name)
        {
            return (FieldRef)FindField(name);
        }

        /// <summary>
        /// Get all the fields for this class
        /// </summary>
        /// <returns>List of fields for this class</returns>
        public FieldRef[] GetFields()
        {
            return (FieldRef[])fields.ToArray(typeof(FieldRef));
        }

        /// <summary>
        /// Add a nested class to this class
        /// </summary>
        /// <param name="name">Nested class name</param>
        /// <returns>Descriptor for the nested class "name"</returns>
        public NestedClassRef AddNestedClass(string name)
        {
            NestedClassRef nestedClass = (NestedClassRef)GetNested(name);
            if (nestedClass != null) DescriptorError(nestedClass);
            nestedClass = new NestedClassRef(this, name);
            AddToClassList(nestedClass);
            return nestedClass;
        }

        /// <summary>
        /// Get the nested class "name"
        /// </summary>
        /// <param name="name">Nestec class name</param>
        /// <returns>Descriptor for the nested class "name"</returns>
        public NestedClassRef GetNestedClass(string name)
        {
            // check nested names
            return (NestedClassRef)GetNested(name);
        }

        /// <summary>
        /// Make this Class exported from an Assembly (ie. add to ExportedType table)
        /// </summary>
        public void MakeExported()
        {
            if ((scope == null) || (!(scope is ModuleRef)))
                throw new Exception("Module not set for class to be exported");
            ((ModuleRef)scope).AddToExportedClassList(this);
        }

        /// <summary>
        /// Get the scope or "parent" of this ClassRef (either ModuleRef or AssemblyRef)
        /// </summary>
        /// <returns>Descriptor for the scope containing this class</returns>
        public virtual ReferenceScope GetScope()
        {
            return scope;
        }

        public override MetaDataElement GetParent() { return scope; }

        /*----------------------------- internal functions ------------------------------*/

        internal void SetExternClass(ExternClass eClass) { externClass = eClass; }

        internal void SetScope(ReferenceScope scope)
        {
            this.scope = scope;
        }

        internal void AddField(FieldRef fld)
        {
            fields.Add(fld);
            fld.SetParent(this);
        }

        internal void AddMethod(MethodRef meth)
        {
            MethodRef m = (MethodRef)GetMethodDesc(meth.Name(), meth.GetParTypes());
            if (m == null)
            {
                methods.Add(meth);
                meth.SetParent(this);
            }
        }

        /*
        internal FieldRef GetExistingField(string fName, uint tyIx, PEReader buff) {
          FieldRef existing = (FieldRef)FindField(fName);
          if (existing != null) {
            Type fType = buff.GetBlobType(tyIx);
            if (!fType.SameType(existing.GetFieldType()))
              throw new DescriptorException("Cannot have two fields (" + fName +
                ") for class " + name);
          }
          return existing;
        }
        */

        /*
        internal MethodRef CheckForMethod(string mName, uint sigIx, PEReader buff) {
          int exIx = FindMeth(mName,0);
          if (exIx > -1) {
            MethSig mType = buff.ReadMethSig(sigIx);
            mType.name = mName;
            exIx = FindMeth(mType,0);
            if (exIx > -1) 
              return (MethodRef)methods[exIx];
          }
          return null;
        }
        */

        internal override string ClassName()
        {
            string nameString = nameSpace + "." + name;
            if ((scope != null) && (scope is AssemblyRef))
                nameString += (", " + ((AssemblyRef)scope).AssemblyString());
            return nameString;
        }

        internal bool HasParent(uint tok)
        {
            return resScopeIx == tok;
        }

        internal override void BuildTables(MetaDataOut md)
        {
            if (!special)
            {
                md.AddToTable(MDTable.TypeRef, this);
                nameIx = md.AddToStringsHeap(name);
                nameSpaceIx = md.AddToStringsHeap(nameSpace);
            }
            scope.BuildMDTables(md);
        }

        internal override void BuildCILInfo(CILWriter output)
        {
            if (!special && scope != null)
            {
                output.AddRef(scope);
            }
        }

        internal static uint Size(MetaData md)
        {
            return md.CodedIndexSize(CIx.ResolutionScope) + 2 * md.StringsIndexSize();
        }

        internal override void Write(PEWriter output)
        {
            output.WriteCodedIndex(CIx.ResolutionScope, scope);
            output.StringsIndex(nameIx);
            output.StringsIndex(nameSpaceIx);
        }

        internal override void WriteType(CILWriter output)
        {
            if ((nameSpace == null) || (nameSpace == ""))
            {
                output.Write("[" + scope.Name() + "]" + name);
            }
            else
            {
                output.Write("[" + scope.Name() + "]" + nameSpace + "." + name);
            }
        }

        internal override sealed uint TypeDefOrRefToken()
        {
            uint cIx = Row;
            cIx = (cIx << 2) | 0x1;
            return cIx;
        }

        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.TypeDefOrRef): return 1;
                case (CIx.HasCustomAttr): return 2;
                case (CIx.MemberRefParent): return 1;
                case (CIx.ResolutionScope): return 3;
            }
            return 0;
        }

        internal override string NameString()
        {
            string nameString = "";
            if (scope != null) nameString = "[" + scope.NameString() + "]";
            if ((nameSpace != null) && (nameSpace.Length > 0)) nameString += nameSpace + ".";
            nameString += name;
            return nameString;
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a reference to a Nested Class
    /// </summary>
    public class NestedClassRef : ClassRef
    {
        ClassRef parent;
        internal uint parentIx = 0;

        /*-------------------- Constructors ---------------------------------*/

        internal NestedClassRef(ClassRef parent, string name)
            : base(parent.GetScope(), "", name)
        {
            this.parent = parent;
        }

        internal NestedClassRef(uint scopeIx, string nsName, string name)
            : base(scopeIx, nsName, name)
        {
        }

        internal NestedClassRef(ReferenceScope scope, string nsName, string name)
            : base(scope, nsName, name)
        {
        }

        internal override void ResolveParent(PEReader buff, bool isExtern)
        {
            if (parent != null) return;
            CIx cIx = CIx.ResolutionScope;
            if (isExtern) cIx = CIx.Implementation;
            parent = (ClassRef)buff.GetCodedElement(cIx, resScopeIx);
            parent.ResolveParent(buff, isExtern);
            parent = (ClassRef)buff.GetCodedElement(cIx, resScopeIx);
            if (parent == null) return;
            NestedClassRef existing = parent.GetNestedClass(name);
            if (existing == null)
            {
                scope = parent.GetScope();
                parent.AddToClassList(this);
            }
            else if (isExtern)
                buff.InsertInTable(MDTable.ExportedType, Row, existing);
            else
                buff.InsertInTable(MDTable.TypeRef, Row, existing);
        }

        /// <summary>
        /// Get the scope of this ClassRef (either ModuleRef or AssemblyRef)
        /// </summary>
        /// <returns>Descriptor for the scope containing this class</returns>
        public override ReferenceScope GetScope()
        {
            if (scope == null)
                scope = parent.GetScope();
            return scope;
        }

        /// <summary>
        /// Get the parent (enclosing ClassRef) for this nested class
        /// </summary>
        /// <returns>Enclosing class descriptor</returns>
        public ClassRef GetParentClass() { return parent; }

        internal void SetParent(ClassRef paren) { parent = paren; }

        internal override string ClassName()
        {
            string nameString = name;
            if (parent != null) nameString = parent.TypeName() + "+" + name;
            if ((scope != null) && (scope is AssemblyRef))
                nameString += (", " + ((AssemblyRef)scope).AssemblyString());
            return nameString;
        }

        internal override string NameString()
        {
            if (parent == null) return name;
            return parent.NameString() + "+" + name;
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            if (!special)
            {
                md.AddToTable(MDTable.TypeRef, this);
                nameIx = md.AddToStringsHeap(name);
                nameSpaceIx = md.AddToStringsHeap(nameSpace);
            }
            parent.BuildMDTables(md);
        }

        internal sealed override void Write(PEWriter output)
        {
            output.WriteCodedIndex(CIx.ResolutionScope, parent);
            output.StringsIndex(nameIx);
            output.StringsIndex(nameSpaceIx);
        }


    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a class defined in System (mscorlib)
    /// </summary>
    internal class SystemClass : ClassRef
    {
        PrimitiveType elemType;
        internal bool added = false;

        internal SystemClass(AssemblyRef paren, PrimitiveType eType)
            : base(paren, "System", eType.GetName())
        {
            elemType = eType;
        }

        //   internal override sealed void AddTypeSpec(MetaDataOut md) {
        //     elemType.AddTypeSpec(md);
        //      if (typeSpec == null) typeSpec = (TypeSpec)elemType.GetTypeSpec(md);
        //      return typeSpec;
        //   }

        internal sealed override void TypeSig(MemoryStream str)
        {
            str.WriteByte(elemType.GetTypeIndex());
        }

        internal override bool SameType(Type tstType)
        {
            if (tstType is SystemClass)
                return elemType == ((SystemClass)tstType).elemType;
            return elemType == tstType;
        }
    }

}