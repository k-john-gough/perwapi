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
    // This Class produces entries in the TypeDef table of the MetaData 
    // in the PE meta data.

    // NOTE:  Entry 0 in TypeDef table is always the pseudo class <module> 
    // which is the parent for functions and variables declared a module level

    /// <summary>
    /// The descriptor for a class defined in the IL (.class) in the current assembly/module
    /// </summary>
    /// 
    public class ClassDef : ClassDesc
    {
        private static readonly uint HasSecurity = 0x00040000;
        private static readonly uint NoSecurity = 0xFFFBFFFF;
        private static readonly uint VisibilityMask = 0x07;
        private static readonly uint LayoutMask = 0x18;
        private static readonly uint StringFormatMask = 0x030000;
        //private static readonly uint fieldListIx = 0, methListIx = 1, eventListIx = 2, propListIx = 3; 
        //private static readonly uint	numListIx = 4;

        protected PEFile scope;
        uint flags;
        Class superType;
        ArrayList security;
        ClassLayout layout;
        uint extendsIx;
        internal ClassRef refOf;
        internal uint eventIx = 0, propIx = 0;
        ArrayList events = new ArrayList();
        ArrayList properties = new ArrayList();
        ArrayList interfaces = new ArrayList();
        ArrayList methodImpls = new ArrayList();
        //uint[] interfaceIndexes;
        //private string[] eventNames, propertyNames, nestedNames;
        //internal string[][] names = new string[numListIx][];

        /*-------------------- Constructors ---------------------------------*/

        internal ClassDef(PEFile scope, TypeAttr attrSet, string nsName, string name)
            : base(nsName, name)
        {
            this.scope = scope;
            superType = MSCorLib.mscorlib.ObjectClass;
            flags = (uint)attrSet;
            tabIx = MDTable.TypeDef;
        }

        internal ClassDef(PEReader buff, uint row, bool isMSCorLib)
        {
            flags = buff.ReadUInt32();
            name = buff.GetString();
            nameSpace = buff.GetString();
            extendsIx = buff.GetCodedIndex(CIx.TypeDefOrRef);
            fieldIx = buff.GetIndex(MDTable.Field);
            methodIx = buff.GetIndex(MDTable.Method);
            this.Row = row;
            tabIx = MDTable.TypeDef;
            if (isMSCorLib && (name == "ValueType"))
                typeIndex = (byte)ElementType.ValueType;
        }

        internal static void Read(PEReader buff, TableRow[] typeDefs, bool isMSCorLib)
        {
            ClassDef prevDef = null;
            prevDef = new ClassDef(buff, 1, isMSCorLib);
            typeDefs[0] = prevDef;
            for (int i = 1; i < typeDefs.Length; i++)
            {
                ClassDef typeDef = new ClassDef(buff, (uint)i + 1, isMSCorLib);
                prevDef.fieldEndIx = typeDef.fieldIx;
                prevDef.methodEndIx = typeDef.methodIx;
                prevDef = typeDef;
                typeDefs[i] = typeDef;
            }
            prevDef.fieldEndIx = buff.GetTableSize(MDTable.Field) + 1;
            prevDef.methodEndIx = buff.GetTableSize(MDTable.Method) + 1;
        }

        private static uint GetParentClassIx(uint[] enclClasses, uint[] nestClasses, uint classIx)
        {
            if (enclClasses == null) return 0;
            for (uint i = 0; i < enclClasses.Length; i++)
            {
                if (nestClasses[i] == classIx)
                    return enclClasses[i];
            }
            return 0;
        }

        internal static void GetClassRefs(PEReader buff, TableRow[] typeRefs, ReferenceScope paren, uint[] parIxs)
        {
            int num = typeRefs.Length;
            uint[] fieldStart = new uint[num + 1], methStart = new uint[num + 1], extends = new uint[num + 1];
            for (int i = 0; i < num; i++)
            {
                uint flags = buff.ReadUInt32();
                string name = buff.GetString();
                string nameSpace = buff.GetString();
                extends[i] = buff.GetCodedIndex(CIx.TypeDefOrRef);
                fieldStart[i] = buff.GetIndex(MDTable.Field);
                methStart[i] = buff.GetIndex(MDTable.Method);
                //Console.WriteLine("flags = " + Hex.Int(flags));
                if (i == 0) // ASSERT first entry is always <Module>
                    typeRefs[i] = paren.GetDefaultClass();
                else if (isPublic(flags))
                {
                    if (parIxs[i] != 0)
                    {
                        typeRefs[i] = new NestedClassRef(paren, nameSpace, name);
                    }
                    else
                    {
                        typeRefs[i] = paren.GetExistingClass(nameSpace, name);
                        if (typeRefs[i] == null)
                        {
                            typeRefs[i] = new ClassRef(paren, nameSpace, name);
                            paren.AddToClassList((ClassRef)typeRefs[i]);
                        }
                    }
                }
            }
            fieldStart[num] = buff.GetTableSize(MDTable.Field) + 1;
            methStart[num] = buff.GetTableSize(MDTable.Method) + 1;
            // Find Nested Classes
            for (int i = 0; i < typeRefs.Length; i++)
            {
                if ((typeRefs[i] != null) && (typeRefs[i] is NestedClassRef))
                {
                    NestedClassRef nRef = (NestedClassRef)typeRefs[i];
                    ClassRef nPar = (ClassRef)typeRefs[parIxs[i] - 1];
                    if (nPar == null)
                    {  // parent is private, so ignore
                        typeRefs[i] = null;
                    }
                    else
                    {
                        nRef.SetParent(nPar);
                        nPar.AddToClassList(nRef);
                    }
                }
                if (typeRefs[i] != null)
                {
                    if (buff.GetCodedElement(CIx.TypeDefOrRef, extends[i]) == MSCorLib.mscorlib.ValueType())
                        ((ClassRef)typeRefs[i]).MakeValueClass();
                    buff.SetElementPosition(MDTable.Field, fieldStart[i]);
                    FieldDef.GetFieldRefs(buff, fieldStart[i + 1] - fieldStart[i], (ClassRef)typeRefs[i]);
                    buff.SetElementPosition(MDTable.Method, methStart[i]);
                    MethodDef.GetMethodRefs(buff, methStart[i + 1] - methStart[i], (ClassRef)typeRefs[i]);
                }
            }
        }

        internal override void Resolve(PEReader buff)
        {
            buff.currentClassScope = this;
            superType = (Class)buff.GetCodedElement(CIx.TypeDefOrRef, extendsIx);
            if ((superType != null) && superType.isValueType())
                typeIndex = (byte)ElementType.ValueType;
            for (int i = 0; fieldIx < fieldEndIx; i++, fieldIx++)
            {
                FieldDef field = (FieldDef)buff.GetElement(MDTable.Field, fieldIx);
                field.SetParent(this);
                fields.Add(field);
            }
            for (int i = 0; methodIx < methodEndIx; i++, methodIx++)
            {
                MethodDef meth = (MethodDef)buff.GetElement(MDTable.Method, methodIx);
                if (Diag.DiagOn) Console.WriteLine("Adding method " + meth.Name() + " to class " + name);
                meth.SetParent(this);
                methods.Add(meth);
            }
            buff.currentClassScope = null;
        }

        internal void ChangeRefsToDefs(ClassDef newType, ClassDef[] oldTypes)
        {
            for (int i = 0; i < oldTypes.Length; i++)
            {
                for (int j = 0; j < oldTypes[i].fields.Count; j++)
                    ((FieldDef)oldTypes[i].fields[j]).ChangeRefsToDefs(this, oldTypes);
                for (int j = 0; j < oldTypes[i].methods.Count; j++)
                    ((MethodDef)oldTypes[i].methods[j]).ChangeRefsToDefs(this, oldTypes);
                for (int j = 0; j < oldTypes[i].events.Count; j++)
                    ((Event)oldTypes[i].events[j]).ChangeRefsToDefs(this, oldTypes);
                for (int j = 0; j < oldTypes[i].properties.Count; j++)
                    ((Property)oldTypes[i].properties[j]).ChangeRefsToDefs(this, oldTypes);
                for (int j = 0; j < oldTypes[i].interfaces.Count; j++)
                    ((ClassDef)oldTypes[i].interfaces[j]).ChangeRefsToDefs(this, oldTypes);
                for (int j = 0; j < oldTypes[i].methodImpls.Count; j++)
                    ((MethodImpl)oldTypes[i].methodImpls[j]).ChangeRefsToDefs(this, oldTypes);
                for (int j = 0; j < oldTypes[i].nestedClasses.Count; j++)
                    ((ClassDef)oldTypes[i].nestedClasses[j]).ChangeRefsToDefs(this, oldTypes);
            }
        }

        public void MergeClasses(ClassDef[] classes)
        {
            ChangeRefsToDefs(this, classes);
            for (int i = 0; i < classes.Length; i++)
            {
                fields.AddRange(classes[i].fields);
                methods.AddRange(classes[i].methods);
                events.AddRange(classes[i].events);
                properties.AddRange(classes[i].properties);
                interfaces.AddRange(classes[i].interfaces);
                methodImpls.AddRange(classes[i].methodImpls);
                nestedClasses.AddRange(classes[i].nestedClasses);
            }
        }

        /*------------------------- public set and get methods --------------------------*/

        /// <summary>
        /// Fetch the PEFile which contains this class
        /// </summary>
        /// <returns>PEFile containing this class</returns>
        public virtual PEFile GetScope() { return scope; }

        public override MetaDataElement GetParent() { return scope; }

        /// <summary>
        /// Fetch or Get the superType for this class
        /// </summary>
        public Class SuperType
        {
            get { return superType; }
            set
            {
                superType = value;
                if (value == MSCorLib.mscorlib.ValueType())
                    typeIndex = (byte)ElementType.ValueType;
                else
                    typeIndex = (byte)ElementType.Class;
            }
        }

        /*    /// <summary>
            /// Make this class inherit from ValueType
            /// </summary>
            public override void MakeValueClass() {
              superType = MSCorLib.mscorlib.ValueType();
              typeIndex = (byte)ElementType.ValueType;
            }
            */

        /// <summary>
        /// Add an attribute to the attributes of this class
        /// </summary>
        /// <param name="ta">the attribute to be added</param>
        public void AddAttribute(TypeAttr ta)
        {
            flags |= (uint)ta;
        }

        /// <summary>
        /// Set the attributes of this class
        /// </summary>
        /// <param name="ta">class attributes</param>
        public void SetAttribute(TypeAttr ta)
        {
            flags = (uint)ta;
        }

        /// <summary>
        /// Get the attributes for this class
        /// </summary>
        /// <returns></returns>
        public TypeAttr GetAttributes() { return (TypeAttr)flags; }

        public GenericParam AddGenericParam(string name)
        {
            GenericParam gp = new GenericParam(name, this, genericParams.Count);
            genericParams.Add(gp);
            return gp;
        }

        public int GetGenericParamCount()
        {
            return genericParams.Count;
        }

        public GenericParam GetGenericParam(string name)
        {
            int pos = FindGenericParam(name);
            if (pos == -1) return null;
            return (GenericParam)genericParams[pos];
        }

        public void RemoveGenericParam(string name)
        {
            int pos = FindGenericParam(name);
            if (pos == -1) return;
            DeleteGenericParam(pos);
        }

        public void RemoveGenericParam(int ix)
        {
            if (ix >= genericParams.Count) return;
            DeleteGenericParam(ix);
        }

        public override ClassSpec Instantiate(Type[] genTypes)
        {
            if (genTypes == null) return null;
            if (genericParams.Count == 0)
                throw new Exception("Cannot instantiate non-generic class");
            if (genTypes.Length != genericParams.Count)
                throw new Exception("Wrong number of type parameters for instantiation\nNeeded "
                    + genericParams.Count + " but got " + genTypes.Length);
            return new ClassSpec(this, genTypes);
        }

        /// <summary>
        /// Add an interface that is implemented by this class
        /// </summary>
        /// <param name="iFace">the interface that is implemented</param>
        public void AddImplementedInterface(Class iFace)
        {
            interfaces.Add(new InterfaceImpl(this, iFace));
            //metaData.AddToTable(MDTable.InterfaceImpl,new InterfaceImpl(this,iFace));
        }

        /// <summary>
        /// Get the interfaces implemented by this class
        /// </summary>
        /// <returns>List of implemented interfaces</returns>
        public Class[] GetInterfaces()
        {
            Class[] iFaces = new Class[interfaces.Count];
            for (int i = 0; i < iFaces.Length; i++)
            {
                iFaces[i] = ((InterfaceImpl)interfaces[i]).TheInterface();
            }
            return iFaces;
        }

        //  FIXME: need a Setter for interfaces too!
        public void SetInterfaces(Class[] iFaces)
        {
            interfaces = new ArrayList(iFaces.Length);
            for (int i = 0; i < iFaces.Length; i++)
                interfaces.Add(iFaces[i]);
        }

        /// <summary>
        /// Add a field to this class
        /// </summary>
        /// <param name="name">field name</param>
        /// <param name="fType">field type</param>
        /// <returns>a descriptor for this new field</returns>
        public FieldDef AddField(string name, Type fType)
        {
            FieldDef field = (FieldDef)FindField(name);
            if (field != null)
                throw new DescriptorException("Field " + field.NameString());
            field = new FieldDef(name, fType, this);
            fields.Add(field);
            return field;
        }

        /// <summary>
        /// Add a field to this class
        /// </summary>
        /// <param name="fAtts">attributes for this field</param>
        /// <param name="name">field name</param>
        /// <param name="fType">field type</param>
        /// <returns>a descriptor for this new field</returns>
        public FieldDef AddField(FieldAttr fAtts, string name, Type fType)
        {
            FieldDef field = AddField(name, fType);
            field.SetFieldAttr(fAtts);
            return field;
        }

        /// <summary>
        /// Add a field to this class
        /// </summary>
        /// <param name="f">Descriptor for the field to be added</param>
        public void AddField(FieldDef f)
        {
            FieldDef field = (FieldDef)FindField(f.Name());
            if (field != null)
                throw new DescriptorException("Field " + field.NameString());
            f.SetParent(this);
            fields.Add(f);
        }

        /// <summary>
        /// Get the descriptor for the field of this class named "name"
        /// </summary>
        /// <param name="name">The field name</param>
        /// <returns>The descriptor for field "name"</returns>
        public FieldDef GetField(string name)
        {
            return (FieldDef)FindField(name);
        }

        /// <summary>
        /// Get the fields for this class
        /// </summary>
        /// <returns>List of fields of this class</returns>
        public FieldDef[] GetFields()
        {
            return (FieldDef[])fields.ToArray(typeof(FieldDef));
        }

        /// <summary>
        /// Add a method to this class
        /// </summary>
        /// <param name="name">method name</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">parameters</param>
        /// <returns>a descriptor for this new method</returns>
        public MethodDef AddMethod(string name, Type retType, Param[] pars)
        {
            System.Diagnostics.Debug.Assert(retType != null);
            MethSig mSig = new MethSig(name);
            mSig.SetParTypes(pars);
            MethodDef meth = (MethodDef)GetMethod(mSig);
            if (meth != null)
                throw new DescriptorException("Method " + meth.NameString());
            mSig.retType = retType;
            meth = new MethodDef(this, mSig, pars);
            methods.Add(meth);
            return meth;
        }

        /// <summary>
        /// Add a method to this class
        /// </summary>
        /// <param name="name">method name</param>
        /// <param name="genPars">generic parameters</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">parameters</param>
        /// <returns>a descriptor for this new method</returns>
        public MethodDef AddMethod(string name, GenericParam[] genPars, Type retType, Param[] pars)
        {
            MethodDef meth = AddMethod(name, retType, pars);
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
        /// <param name="mAtts">attributes for this method</param>
        /// <param name="iAtts">implementation attributes for this method</param>
        /// <param name="name">method name</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">parameters</param>
        /// <returns>a descriptor for this new method</returns>
        public MethodDef AddMethod(MethAttr mAtts, ImplAttr iAtts, string name,
            Type retType, Param[] pars)
        {
            MethodDef meth = AddMethod(name, retType, pars);
            meth.AddMethAttribute(mAtts);
            meth.AddImplAttribute(iAtts);
            return meth;
        }
        /// <summary>
        /// Add a method to this class
        /// </summary>
        /// <param name="mAtts">attributes for this method</param>
        /// <param name="iAtts">implementation attributes for this method</param>
        /// <param name="name">method name</param>
        /// <param name="genPars">generic parameters</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">parameters</param>
        /// <returns>a descriptor for this new method</returns>
        public MethodDef AddMethod(MethAttr mAtts, ImplAttr iAtts, string name,
            GenericParam[] genPars, Type retType, Param[] pars)
        {
            MethodDef meth = AddMethod(name, genPars, retType, pars);
            meth.AddMethAttribute(mAtts);
            meth.AddImplAttribute(iAtts);
            return meth;
        }

        /// <summary>
        /// Add a method to this class
        /// </summary>
        /// <param name="meth">Descriptor for the method to be added</param>
        public void AddMethod(MethodDef meth)
        {
            MethodDef m = (MethodDef)GetMethodDesc(meth.Name(), meth.GetParTypes());
            if (m != null)
                throw new DescriptorException("Method " + m.NameString());
            methods.Add(meth);
            meth.SetParent(this);
        }

        /// <summary>
        /// Get the descriptor for the method "name" of this class
        /// </summary>
        /// <param name="name">The name of the method to be retrieved</param>
        /// <returns>The method descriptor for "name"</returns>
        public MethodDef GetMethod(string name)
        {
            return (MethodDef)GetMethodDesc(name);
        }

        /// <summary>
        /// Get the descriptor for the method called "name" with the signature "parTypes"
        /// </summary>
        /// <param name="name">The name of the method</param>
        /// <param name="parTypes">The signature of the method</param>
        /// <returns>The method descriptor for name(parTypes)</returns>
        public MethodDef GetMethod(string name, Type[] parTypes)
        {
            return (MethodDef)GetMethodDesc(name, parTypes);
        }

        /// <summary>
        /// Get all the methods of this class called "name"
        /// </summary>
        /// <param name="name">The method name</param>
        /// <returns>List of methods called "name"</returns>
        public MethodDef[] GetMethods(string name)
        {
            ArrayList meths = GetMeths(name);
            return (MethodDef[])meths.ToArray(typeof(MethodDef));
        }

        /// <summary>
        /// Get all the methods for this class
        /// </summary>
        /// <returns>List of methods for this class</returns>
        public MethodDef[] GetMethods()
        {
            return (MethodDef[])methods.ToArray(typeof(MethodDef));
        }

        /// <summary>
        /// Add an event to this class
        /// </summary>
        /// <param name="name">event name</param>
        /// <param name="eType">event type</param>
        /// <returns>a descriptor for this new event</returns>
        public Event AddEvent(string name, Type eType)
        {
            Event e = (Event)FindFeature(name, events);
            if (e != null)
                throw new DescriptorException("Event " + e.NameString());
            e = new Event(name, eType, this);
            events.Add(e);
            return e;
        }

        /// <summary>
        /// Get the event "name" of this class
        /// </summary>
        /// <param name="name">The event name</param>
        /// <returns>The event desctiptor for "name"</returns>
        public Event GetEvent(string name)
        {
            return (Event)FindFeature(name, events);
        }

        /// <summary>
        /// Get all the events of this class
        /// </summary>
        /// <returns>List of events for this class</returns>
        public Event[] GetEvents()
        {
            return (Event[])events.ToArray(typeof(Event));
        }

        /// <summary>
        /// Remove the event "name" from this class
        /// </summary>
        /// <param name="name">The name of the event to be removed</param>
        public void RemoveEvent(string name)
        {
            Feature ev = FindFeature(name, events);
            if (ev != null) events.Remove(ev);
        }


        /// <summary>
        /// Add a property to this class
        /// </summary>
        /// <param name="name">property name</param>
        /// <param name="pars">parameters</param>
        /// <param name="retType">return type</param>
        /// <returns>a descriptor for this new property</returns>
        public Property AddProperty(string name, Type retType, Type[] pars)
        {
            Property p = (Property)FindFeature(name, properties);
            if (p != null)
                throw new DescriptorException("Property " + p.NameString());
            p = new Property(name, retType, pars, this);
            properties.Add(p);
            return p;
        }


        /// <summary>
        /// Get the property "name" for this class
        /// </summary>
        /// <param name="name">Descriptor for the property "name"</param>
        /// <returns></returns>
        public Property GetProperty(string name)
        {
            return (Property)FindFeature(name, properties);
        }

        /// <summary>
        /// Get all the properties for this class
        /// </summary>
        /// <returns>List of properties for this class</returns>
        public Property[] GetProperties()
        {
            return (Property[])properties.ToArray(typeof(Property));
        }

        /// <summary>
        /// Remove the property "name" from this class
        /// </summary>
        /// <param name="name">Name of the property to be removed</param>
        public void RemoveProperty(string name)
        {
            Feature prop = FindFeature(name, properties);
            if (prop != null) properties.Remove(prop);
        }

        /// <summary>
        /// Add a nested class to this class
        /// </summary>
        /// <param name="attrSet">attributes for this nested class</param>
        /// <param name="name">nested class name</param>
        /// <returns>a descriptor for this new nested class</returns>
        public NestedClassDef AddNestedClass(TypeAttr attrSet, string name)
        {
            NestedClassDef nClass = GetNestedClass(name);
            if (nClass != null)
                throw new DescriptorException("Nested Class " + nClass.NameString());
            nClass = new NestedClassDef(this, attrSet, name);
            nestedClasses.Add(nClass);
            return (nClass);
        }

        /// <summary>
        /// Add a nested class to this class
        /// </summary>
        /// <param name="attrSet">attributes for this nested class</param>
        /// <param name="name">nested class name</param>
        /// <param name="sType">super type of this nested class</param>
        /// <returns>a descriptor for this new nested class</returns>
        public NestedClassDef AddNestedClass(TypeAttr attrSet, string name, Class sType)
        {
            NestedClassDef nClass = AddNestedClass(attrSet, name);
            nClass.superType = sType;
            return (nClass);
        }

        /// <summary>
        /// Get the nested class called "name"
        /// </summary>
        /// <param name="name">The name of the nested class</param>
        /// <returns>Descriptor for the nested class</returns>
        public NestedClassDef GetNestedClass(string name)
        {
            //CheckNestedClassNames(MDTable.TypeDef);
            return (NestedClassDef)GetNested(name);
        }

        /// <summary>
        /// Add layout information for this class.  This class must have the
        /// sequential or explicit attribute.
        /// </summary>
        /// <param name="packSize">packing size (.pack)</param>
        /// <param name="classSize">class size (.size)</param>
        public void AddLayoutInfo(int packSize, int classSize)
        {
            layout = new ClassLayout(packSize, classSize, this);
        }

        /// <summary>
        /// Get the pack size for this class (only valid for ExplicitLayout or SequentialLayout
        /// </summary>
        /// <returns>Class pack size</returns>
        public int GetPackSize()
        {
            if ((layout == null) && (((flags & (uint)TypeAttr.ExplicitLayout) != 0) ||
                ((flags & (uint)TypeAttr.SequentialLayout) != 0)) && (buffer != null))
            {
                buffer.SetElementPosition(MDTable.ClassLayout, 0);
                //layout = buffer.FindParent(MDTable.ClassLayout,this);
            }
            if (layout != null) return layout.GetPack();
            return 0;
        }

        /// <summary>
        /// Get the size of this class (only valid for ExplicitLayout or SequentialLayout
        /// </summary>
        /// <returns>The size of this class</returns>
        public int GetClassSize()
        {
            if (layout == null) return 0;
            return layout.GetSize();
        }

        /// <summary>
        /// Get the ClassRef for this ClassDef, if there is one
        /// </summary>
        public ClassRef RefOf
        {
            get
            {
                if (refOf == null)
                {
                    ModuleRef modRef = scope.refOf;
                    if (modRef != null)
                        refOf = modRef.GetClass(name);
                }
                return refOf;
            }
        }

        /// <summary>
        /// Make a ClassRef for this ClassDef
        /// </summary>
        /// <returns>ClassRef equivalent to this ClassDef</returns>
        public virtual ClassRef MakeRefOf()
        {
            if (refOf == null)
            {
                Assembly assem = scope.GetThisAssembly();
                ReferenceScope scopeRef;
                if (assem != null)
                    scopeRef = assem.MakeRefOf();
                else
                    scopeRef = scope.MakeRefOf();

                refOf = scopeRef.GetClass(name);
                if (refOf == null)
                {
                    refOf = new ClassRef(scopeRef, nameSpace, name);
                    scopeRef.AddToClassList(refOf);
                }
                refOf.defOf = this;
            }
            return refOf;
        }

        /// <summary>
        /// Use a method as the implementation for another method (.override)
        /// </summary>
        /// <param name="decl">the method to be overridden</param>
        /// <param name="body">the implementation to be used</param>
        public void AddMethodOverride(Method decl, Method body)
        {
            methodImpls.Add(new MethodImpl(this, decl, body));
        }

        public void AddMethodOverride(MethodImpl mImpl)
        {
            methodImpls.Add(mImpl);
            mImpl.SetOwner(this);
        }

        public MethodImpl[] GetMethodOverrides()
        {
            return (MethodImpl[])methodImpls.ToArray(typeof(MethodImpl));
        }

        public void RemoveMethodOverride(MethodImpl mImpl)
        {
            if (methodImpls != null)
                methodImpls.Remove(mImpl);
        }


        /// <summary>
        /// Add security to this class
        /// </summary>
        /// <param name="act">The security action</param>
        /// <param name="permissionSet">Permission set</param>
        public void AddSecurity(SecurityAction act, byte[] permissionSet)
        {
            AddSecurity(new DeclSecurity(this, act, permissionSet));
            // securityActions = permissionSet;
        }

        /// <summary>
        /// Add security to this class
        /// </summary>
        /// <param name="sec">The descriptor for the security to add to this class</param>
        internal void AddSecurity(DeclSecurity sec)
        {
            flags |= HasSecurity;
            if (security == null) security = new ArrayList();
            security.Add(sec);
        }

        /// <summary>
        /// Get the security descriptor associated with this class
        /// </summary>
        /// <returns></returns>
        public DeclSecurity[] GetSecurity()
        {
            if (security == null) return null;
            return (DeclSecurity[])security.ToArray(typeof(DeclSecurity));
        }

        /// <summary>
        /// Remove the security associated with this class
        /// </summary>
        public void DeleteSecurity()
        {
            flags &= NoSecurity;
            security = null;
        }

        //public void AddLineInfo(int row, int col) { }

        /*----------------------------- internal functions ------------------------------*/

        internal bool isPublic()
        {
            uint vis = flags & VisibilityMask;
            return (vis > 0) && (vis != 3) && (vis != 5);
        }

        internal static bool isPublic(uint flags)
        {
            uint vis = flags & VisibilityMask;
            return (vis > 0) && (vis != 3) && (vis != 5);
        }

        internal static bool isNested(uint flags)
        {
            uint vis = flags & VisibilityMask;
            return vis > 1;
        }

        internal override bool isDef() { return true; }

        private Feature FindFeature(string name, ArrayList featureList)
        {
            if (featureList == null) return null;
            for (int i = 0; i < featureList.Count; i++)
            {
                if (((Feature)featureList[i]).Name() == name)
                {
                    return (Feature)featureList[i];
                }
            }
            return null;
        }

        private int FindGenericParam(string name)
        {
            for (int i = 0; i < genericParams.Count; i++)
            {
                GenericParam gp = (GenericParam)genericParams[i];
                if (gp.GetName() == name) return i;
            }
            return -1;
        }

        internal ClassLayout Layout
        {
            set { layout = value; }
            get { return layout; }
        }

        internal void SetScope(PEFile mod) { scope = mod; }

        internal void AddImplementedInterface(InterfaceImpl iImpl)
        {
            interfaces.Add(iImpl);
        }

        internal NestedClassDef MakeNestedClass(ClassDef parent)
        {
            NestedClassDef nClass = new NestedClassDef(parent, (TypeAttr)flags, name);
            ClassDef tmp = nClass;
            tmp.fieldIx = fieldIx;
            tmp.fieldEndIx = fieldEndIx;
            tmp.methodIx = methodIx;
            tmp.methodEndIx = methodEndIx;
            tmp.extendsIx = extendsIx;
            tmp.Row = Row;
            parent.nestedClasses.Add(nClass);
            return nClass;
        }

        private void ReadSecurity()
        {
            //if ((security == null) && ((flags & HasSecurity) != 0) && (buffer != null)) 
            //security = buffer.FindParent(MDTable.DeclSecurity,this);
        }

        public override void MakeSpecial()
        {
            special = true;
            superType = null;
            flags = (uint)TypeAttr.Private;
        }

        internal void AddMethodImpl(MethodImpl impl)
        {
            methodImpls.Add(impl);
        }

        internal void AddEvent(Event ev)
        {
            if (ev == null) return;
            ev.SetParent(this);
            events.Add(ev);
        }

        internal void AddProperty(Property prop)
        {
            if (prop == null) return;
            prop.SetParent(this);
            properties.Add(prop);
        }

        internal void AddToFeatureList(ArrayList list, MDTable tabIx)
        {
            if (tabIx == MDTable.Event)
            {
                events.AddRange(list);
            }
            else
            {
                properties.AddRange(list);
            }
        }

        // fix for Whidbey bug
        internal void AddGenericsToTable(MetaDataOut md)
        {
            //for (int i=0; i < methods.Count; i++) {
            //  ((MethodDef)methods[i]).AddGenericsToTable(md);
            //}
            for (int i = 0; i < genericParams.Count; i++)
            {
                md.AddToTable(MDTable.GenericParam, (GenericParam)genericParams[i]);
            }
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.TypeDef, this);
            //if ((flags & (uint)TypeAttr.Interface) != 0) { superType = null; }
            if (superType != null)
            {
                superType.BuildMDTables(md);
                if (superType is ClassSpec) md.AddToTable(MDTable.TypeSpec, superType);
            }
            for (int i = 0; i < genericParams.Count; i++)
            {
                ((GenericParam)genericParams[i]).BuildMDTables(md);
            }
            nameIx = md.AddToStringsHeap(name);
            nameSpaceIx = md.AddToStringsHeap(nameSpace);
            if (security != null)
            {
                for (int i = 0; i < security.Count; i++)
                {
                    ((DeclSecurity)security[i]).BuildMDTables(md);
                }
            }
            // Console.WriteLine("Building tables for " + name);
            if (layout != null) layout.BuildMDTables(md);
            // Console.WriteLine("adding methods " + methods.Count);
            methodIx = md.TableIndex(MDTable.Method);
            for (int i = 0; i < methods.Count; i++)
            {
                ((MethodDef)methods[i]).BuildMDTables(md);
            }
            // Console.WriteLine("adding fields");
            fieldIx = md.TableIndex(MDTable.Field);
            for (int i = 0; i < fields.Count; i++)
            {
                ((FieldDef)fields[i]).BuildMDTables(md);
            }
            // Console.WriteLine("adding interfaceimpls and methodimpls");
            if (interfaces.Count > 0)
            {
                for (int i = 0; i < interfaces.Count; i++)
                {
                    ((InterfaceImpl)interfaces[i]).BuildMDTables(md);
                }
            }
            if (methodImpls.Count > 0)
            {
                for (int i = 0; i < methodImpls.Count; i++)
                {
                    ((MethodImpl)methodImpls[i]).BuildMDTables(md);
                }
            }
            // Console.WriteLine("adding events and properties");
            if (events.Count > 0)
            {
                new MapElem(this, md.TableIndex(MDTable.Event), MDTable.EventMap).BuildMDTables(md);
                for (int i = 0; i < events.Count; i++)
                {
                    ((Event)events[i]).BuildMDTables(md);
                }
            }
            if (properties.Count > 0)
            {
                new MapElem(this, md.TableIndex(MDTable.Property), MDTable.PropertyMap).BuildMDTables(md);
                for (int i = 0; i < properties.Count; i++)
                {
                    ((Property)properties[i]).BuildMDTables(md);
                }
            }
            // Console.WriteLine("Adding nested classes");
            if (nestedClasses.Count > 0)
            {
                for (int i = 0; i < nestedClasses.Count; i++)
                {
                    ClassDef nClass = (ClassDef)nestedClasses[i];
                    nClass.BuildMDTables(md);
                    new MapElem(nClass, this, MDTable.NestedClass).BuildTables(md);
                }
            }
            // Console.WriteLine("End of building tables");
        }

        internal override void BuildCILInfo(CILWriter output)
        {
            if ((superType != null) && !(superType is ClassDef))
            {
                superType.BuildCILInfo(output);
            }
            for (int i = 0; i < genericParams.Count; i++)
            {
                ((GenericParam)genericParams[i]).BuildCILInfo(output);
            }
            if (security != null)
            {
                for (int i = 0; i < security.Count; i++)
                {
                    ((DeclSecurity)security[i]).BuildCILInfo(output);
                }
            }
            // Console.WriteLine("Building CIL info for " + name);
            // Console.WriteLine("adding methods " + methods.Count);
            for (int i = 0; i < methods.Count; i++)
            {
                ((MethodDef)methods[i]).BuildCILInfo(output);
            }
            // Console.WriteLine("adding fields");
            for (int i = 0; i < fields.Count; i++)
            {
                ((FieldDef)fields[i]).BuildCILInfo(output);
            }
            // Console.WriteLine("adding interfaceimpls and methodimpls");
            if (interfaces.Count > 0)
            {
                for (int i = 0; i < interfaces.Count; i++)
                {
                    ((InterfaceImpl)interfaces[i]).BuildCILInfo(output);
                }
            }
            if (methodImpls.Count > 0)
            {
                for (int i = 0; i < methodImpls.Count; i++)
                {
                    ((MethodImpl)methodImpls[i]).BuildCILInfo(output);
                }
            }
            for (int i = 0; i < events.Count; i++)
            {
                ((Event)events[i]).BuildCILInfo(output);
            }
            for (int i = 0; i < properties.Count; i++)
            {
                ((Property)properties[i]).BuildCILInfo(output);
            }
            // Console.WriteLine("Adding nested classes");
            for (int i = 0; i < nestedClasses.Count; i++)
            {
                ((ClassDef)nestedClasses[i]).BuildCILInfo(output);
            }
            // Console.WriteLine("End of building tables");      
        }

        internal static uint Size(MetaData md)
        {
            return 4 + 2 * md.StringsIndexSize() +
                md.CodedIndexSize(CIx.TypeDefOrRef) +
                md.TableIndexSize(MDTable.Field) +
                md.TableIndexSize(MDTable.Method);
        }

        internal sealed override void Write(PEWriter output)
        {
            output.Write(flags);
            output.StringsIndex(nameIx);
            output.StringsIndex(nameSpaceIx);
            //if (superType != null) 
            // Console.WriteLine("getting coded index for superType of " + name + " = " + superType.GetCodedIx(CIx.TypeDefOrRef));
            output.WriteCodedIndex(CIx.TypeDefOrRef, superType);
            output.WriteIndex(MDTable.Field, fieldIx);
            output.WriteIndex(MDTable.Method, methodIx);
        }

        internal override void WriteType(CILWriter output)
        {
            output.Write("class ");
            WriteName(output);
        }

        internal override void WriteName(CILWriter output)
        {
            if ((nameSpace == null) || (nameSpace == ""))
            {
                output.Write(name);
            }
            else
            {
                output.Write(nameSpace + "." + name);
            }
        }


        private void WriteFlags(CILWriter output)
        {
            uint vis = flags & VisibilityMask;
            switch (vis)
            {
                case 0: output.Write("private "); break;
                case 1: output.Write("public "); break;
                case 2: output.Write("nested public "); break;
                case 3: output.Write("nested private "); break;
                case 4: output.Write("nested family "); break;
                case 5: output.Write("nested assembly "); break;
                case 6: output.Write("nested famandassem "); break;
                case 7: output.Write("nested famorassem "); break;
            }
            uint layout = flags & LayoutMask;
            if (layout == 0)
            {
                output.Write("auto ");
            }
            else if (layout == (uint)TypeAttr.ExplicitLayout)
            {
                output.Write("explicit ");
            }
            else
            {
                output.Write("sequential ");
            }
            if ((flags & (uint)TypeAttr.Interface) != 0)
            {
                output.Write("interface ");
            }
            if ((flags & (uint)TypeAttr.Abstract) != 0)
            {
                output.Write("abstract ");
            }
            else if ((flags & (uint)TypeAttr.Sealed) != 0)
            {
                output.Write("sealed ");
            }
            uint strForm = flags & StringFormatMask;
            if (strForm == 0)
            {
                output.Write("ansi ");
            }
            else if (strForm == (uint)TypeAttr.UnicodeClass)
            {
                output.Write("unicode ");
            }
            else
            {
                output.Write("autochar ");
            }
            if ((flags & (uint)TypeAttr.BeforeFieldInit) != 0)
            {
                output.Write("beforefieldinit ");
            }
            if ((flags & (uint)TypeAttr.Serializable) != 0)
            {
                output.Write("serializable ");
            }
            if ((flags & (uint)TypeAttr.SpecialName) != 0)
            {
                output.Write("specialname ");
            }
            if ((flags & (uint)TypeAttr.RTSpecialName) != 0)
            {
                output.Write("rtsspecialname ");
            }
        }

        internal override void Write(CILWriter output)
        {
            output.Write(".class ");
            WriteFlags(output);
            if ((nameSpace != null) && (nameSpace != ""))
            {
                output.Write(nameSpace + ".");
            }
            output.WriteLine(name);
            if (superType != null)
            {
                output.Write("    extends ");
                superType.WriteName(output);
            }
            if (interfaces.Count > 0)
            {
                output.Write("  implements ");
                for (int i = 0; i < interfaces.Count; i++)
                {
                    InterfaceImpl impl = (InterfaceImpl)interfaces[i];
                    if (i > 0) output.Write(", ");
                    impl.TheInterface().WriteName(output);
                }
            }
            output.WriteLine();
            output.WriteLine("{");
            for (int i = 0; i < fields.Count; i++)
            {
                ((Field)fields[i]).Write(output);
                output.WriteLine();
            }
            for (int i = 0; i < methods.Count; i++)
            {
                ((MethodDef)methods[i]).Write(output);
                output.WriteLine();
            }
            for (int i = 0; i < methodImpls.Count; i++)
            {
                ((MethodImpl)methodImpls[i]).Write(output);
                output.WriteLine();
            }
            for (int i = 0; i < events.Count; i++)
            {
                ((Event)events[i]).Write(output);
                output.WriteLine();
            }
            for (int i = 0; i < properties.Count; i++)
            {
                ((Property)properties[i]).Write(output);
                output.WriteLine();
            }

            output.WriteLine("}");
            output.WriteLine();
        }


        internal sealed override uint TypeDefOrRefToken()
        {
            uint cIx = Row;
            cIx = cIx << 2;
            return cIx;
        }

        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.TypeDefOrRef): return 0;
                case (CIx.HasCustomAttr): return 3;
                case (CIx.HasDeclSecurity): return 0;
                case (CIx.TypeOrMethodDef): return 0;
            }
            return 0;
        }

        internal override string ClassName()
        {
            return (nameSpace + "." + name);
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
    /// Descriptor for a Nested Class defined in an assembly
    /// </summary>
    public class NestedClassDef : ClassDef
    {
        ClassDef parent;

        /*-------------------- Constructors ---------------------------------*/

        internal NestedClassDef(ClassDef parent, TypeAttr attrSet, string name)
            : base(parent.GetScope(), attrSet, "", name)
        {
            this.parent = parent;
        }

        /// <summary>
        /// Fetch the PEFile which contains this class
        /// </summary>
        /// <returns>PEFile containing this class</returns>
        public override PEFile GetScope()
        {
            if (scope == null)
                scope = parent.GetScope();
            return scope;
        }

        /// <summary>
        /// Get the enclosing class for this nested class
        /// </summary>
        /// <returns>ClassDef of the enclosing class</returns>
        public ClassDef GetParentClass() { return parent; }

        internal void SetParent(ClassDef par) { parent = par; }

        internal override string ClassName()
        {
            string nameString = name;
            if (parent != null) nameString = parent.TypeName() + "+" + name;
            return nameString;
        }

        /// <returns>ClassRef equivalent to this ClassDef</returns>
        public override ClassRef MakeRefOf()
        {
            if (refOf == null)
            {
                ClassRef parentRef = parent.MakeRefOf();
                refOf = parentRef.GetNestedClass(name);
                if (refOf == null)
                {
                    refOf = parentRef.AddNestedClass(name);
                }
                refOf.defOf = this;
            }
            return refOf;
        }

    }


}