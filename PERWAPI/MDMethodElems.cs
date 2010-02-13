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
    /// Base class for Method Descriptors
    /// </summary>
    public abstract class Method : Member
    {
        protected MethSig sig;
        protected ArrayList genericParams;

        /*-------------------- Constructors ---------------------------------*/

        internal Method(string methName, Type rType, Class paren)
            : base(methName, paren)
        {
            sig = new MethSig(methName);
            sig.retType = rType;
        }

        internal Method(string name) : base(name) { }

        /// <summary>
        /// Add calling conventions to this method descriptor
        /// </summary>
        /// <param name="cconv"></param>
        public void AddCallConv(CallConv cconv)
        {
            sig.callConv |= cconv;
        }

        /// <summary>
        /// Get the calling conventions for this method
        /// </summary>
        /// <returns></returns>
        public CallConv GetCallConv()
        {
            return sig.callConv;
        }

        /// <summary>
        /// Set the return type
        /// </summary>
        /// <param name="retT">type returned</param>
        internal void AddRetType(Type retT)
        {
            System.Diagnostics.Debug.Assert(retT != null);
            sig.retType = retT;
        }

        /// <summary>
        /// Get the method return type
        /// </summary>
        /// <returns>method return type</returns>
        public Type GetRetType()
        {
            return sig.retType;
        }

        /// <summary>
        /// Get the types of the method parameters
        /// </summary>
        /// <returns>list of parameter types</returns>
        public Type[] GetParTypes()
        {
            return sig.parTypes;
        }

        /// <summary>
        /// Get the optional parameter types (for varargs)
        /// </summary>
        /// <returns>list of vararg types</returns>
        public Type[] GetOptParTypes()
        {
            return sig.optParTypes;
        }

        public int GetGenericParamCount()
        {
            return genericParams == null ? 0 : genericParams.Count;
        }

        /// <summary>
        /// Add a generic type to this method
        /// </summary>
        /// <param name="name">the name of the generic type</param>
        /// <returns>the descriptor for the generic type</returns>
        public GenericParam AddGenericParam(string name)
        {
            if (genericParams == null) genericParams = new ArrayList();
            GenericParam gp = new GenericParam(name, this, genericParams.Count);
            sig.callConv |= CallConv.Generic;
            genericParams.Add(gp);
            sig.numGenPars = (uint)genericParams.Count;
            return gp;
        }

        /// <summary>
        /// Get the descriptor for a generic type 
        /// </summary>
        /// <param name="name">the name of the generic type</param>
        /// <returns>descriptor for generic type "name"</returns>
        public GenericParam GetGenericParam(string name)
        {
            int pos = FindGenericParam(name);
            if (pos == -1) return null;
            return (GenericParam)genericParams[pos];
        }

        public GenericParam GetGenericParam(int ix)
        {
            if ((genericParams == null) || (ix >= genericParams.Count)) return null;
            return (GenericParam)genericParams[ix];
        }

        public void RemoveGenericParam(string name)
        {
            int pos = FindGenericParam(name);
            if (pos == -1) return;
            DeleteGenericParam(pos);
        }

        public void RemoveGenericParam(int ix)
        {
            if (genericParams == null) return;
            if (ix >= genericParams.Count) return;
            DeleteGenericParam(ix);
        }

        public MethodSpec Instantiate(Type[] genTypes)
        {
            if (genTypes == null) return null;
            if ((genericParams == null) || (genericParams.Count == 0))
                throw new Exception("Cannot instantiate non-generic method");
            if (genTypes.Length != genericParams.Count)
                throw new Exception("Wrong number of type parameters for instantiation\nNeeded "
                    + genericParams.Count + " but got " + genTypes.Length);
            return new MethodSpec(this, genTypes);
        }

        public GenericParam[] GetGenericParams()
        {    // KJG June 2005
            if (genericParams == null) return null;
            return (GenericParam[])genericParams.ToArray(typeof(GenericParam));
        }

        /*------------------------- internal functions --------------------------*/

        internal abstract void TypeSig(MemoryStream sig);

        internal bool HasNameAndSig(string name, Type[] sigTypes)
        {
            if (this.name != name) return false;
            return sig.HasSig(sigTypes);
        }

        internal bool HasNameAndSig(string name, Type[] sigTypes, Type[] optPars)
        {
            if (this.name != name) return false;
            return sig.HasSig(sigTypes, optPars);
        }

        internal MethSig GetSig() { return sig; }

        internal void SetSig(MethSig sig)
        {
            this.sig = sig;
            this.sig.name = name;
        }

        internal override string NameString()
        {
            return parent.NameString() + sig.NameString();
        }

        private int FindGenericParam(string name)
        {
            if (genericParams == null) return -1;
            for (int i = 0; i < genericParams.Count; i++)
            {
                GenericParam gp = (GenericParam)genericParams[i];
                if (gp.GetName() == name) return i;
            }
            return -1;
        }

        private void DeleteGenericParam(int pos)
        {
            genericParams.RemoveAt(pos);
            for (int i = pos; i < genericParams.Count; i++)
            {
                GenericParam gp = (GenericParam)genericParams[i];
                gp.Index = (uint)i;
            }
        }

        internal void AddGenericParam(GenericParam par)
        {
            if (genericParams == null) genericParams = new ArrayList();
            genericParams.Add(par);
            //sig.callConv |= CallConv.Generic;
            //sig.numGenPars = (uint)genericParams.Count;
        }

        internal ArrayList GenericParams
        {
            get { return genericParams; }
            set { genericParams = value; }
        }

        internal void SetGenericParams(GenericParam[] pars)
        {
            genericParams = new ArrayList(pars);
            sig.callConv |= CallConv.Generic;
            sig.numGenPars = (uint)genericParams.Count;
        }

        internal override void WriteType(CILWriter output)
        {
            sig.WriteCallConv(output);
            sig.retType.WriteType(output);
            output.Write(" ");
            parent.WriteName(output);
            output.Write("::" + name);
            sig.WriteParTypes(output);
        }


    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for an Instantiation of a generic method 
    /// </summary>
    public class MethodSpec : Method
    {
        Method methParent;
        uint instIx;
        Type[] instTypes;
        internal static byte GENERICINST = 0x0A;

        /*-------------------- Constructors ---------------------------------*/

        public MethodSpec(Method mParent, Type[] instTypes)
            : base(null)
        {
            this.methParent = mParent;
            this.instTypes = instTypes;
            tabIx = MDTable.MethodSpec;
        }

        internal MethodSpec(PEReader buff)
            : base(null)
        {
            parentIx = buff.GetCodedIndex(CIx.MethodDefOrRef);
            instIx = buff.GetBlobIx();
            tabIx = MDTable.MethodSpec;
            this.unresolved = true;
        }

        internal static void Read(PEReader buff, TableRow[] specs)
        {
            for (int i = 0; i < specs.Length; i++)
                specs[i] = new MethodSpec(buff);
        }

        internal override void Resolve(PEReader buff)
        {
            methParent = (Method)buff.GetCodedElement(CIx.MethodDefOrRef, parentIx);
            buff.currentMethodScope = methParent;  // set scopes - Fix by CK
            buff.currentClassScope = (Class)methParent.GetParent();
            instTypes = buff.ReadMethSpecSig(instIx);
            this.unresolved = false;
            buff.currentMethodScope = null;
            buff.currentClassScope = null;
        }

        internal override void TypeSig(MemoryStream str)
        {
            str.WriteByte(GENERICINST);
            MetaDataOut.CompressNum(BlobUtil.CompressUInt((uint)instTypes.Length), str);
            for (int i = 0; i < instTypes.Length; i++)
            {
                instTypes[i].TypeSig(str);
            }
        }

        internal static uint Size(MetaData md)
        {
            return md.CodedIndexSize(CIx.MethodDefOrRef) + md.BlobIndexSize();
        }

        internal override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.MethodSpec, this);
            if (!(methParent is MethodDef)) // Never build a method def
                methParent.BuildMDTables(md);
            for (int i = 0; i < instTypes.Length; i++)
            {
                instTypes[i].BuildMDTables(md);
            }
        }

        internal override void BuildSignatures(MetaDataOut md)
        {
            MemoryStream outSig = new MemoryStream();
            TypeSig(outSig);
            instIx = md.AddToBlobHeap(outSig.ToArray());
        }

        internal override void Write(PEWriter output)
        {
            output.WriteCodedIndex(CIx.MethodDefOrRef, methParent);
            output.BlobIndex(instIx);
        }

        /*-------------------- Public Methods ------------------------------*/

        public Type[] GetGenericParamTypes()
        {  // KJG 15 July 2005
            return instTypes;
        }

        public Method GetMethParent()
        {         // KJG 15 July 2005
            return methParent;
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a method defined in another assembly/module
    /// </summary>
    public class MethodRef : Method
    {
        internal MethodDef defOf;
        MethodDef varArgParent = null;

        /*-------------------- Constructors ---------------------------------*/

        internal MethodRef(Class paren, string name, Type retType, Type[] pars)
            : base(name, retType, paren)
        {
            sig.parTypes = pars;
            if (pars != null) sig.numPars = (uint)pars.Length;

        }

        internal MethodRef(uint parIx, string name, uint sigIx)
            : base(name)
        {
            this.parentIx = parIx;
            this.sigIx = sigIx;
        }

        internal MethodRef(MethSig sig)
            : base(sig.name)
        {
            this.sig = sig;
        }

        internal override void Resolve(PEReader buff)
        {
            if (sig == null)
            {
                buff.currentMethodScope = this;
                buff.currentClassScope = parent;
                sig = buff.ReadMethSig(this, name, sigIx);
                buff.currentMethodScope = null;
                buff.currentClassScope = null;
            }
        }

        internal override Member ResolveParent(PEReader buff)
        {
            if (parent != null) return this;
            buff.currentMethodScope = this;
            MetaDataElement paren = buff.GetCodedElement(CIx.MemberRefParent, parentIx);
            buff.currentMethodScope = null;
            if (paren is MethodDef)
            {
                parent = null;
                varArgParent = (MethodDef)paren;
                //this.sig = buff.ReadMethSig(this,name,sigIx);
                ((MethodDef)paren).AddVarArgSig(this);
                return this;
            }
            else if (paren is ClassSpec)
            {
                ((ClassSpec)paren).AddMethod(this);
                return this;
            }
            else if (paren is PrimitiveType)
            {
                paren = MSCorLib.mscorlib.GetDefaultClass();
            }
            else if (paren is ClassDef)
            {
                this.sig = buff.ReadMethSig(this, name, sigIx);
                return ((ClassDef)paren).GetMethod(this.sig);
            }
            else if (paren is TypeSpec)
            {
                paren = new ConstructedTypeSpec((TypeSpec)paren);
                //Console.WriteLine("Got TypeSpec as parent of Member");
                //return this;
                //throw new Exception("Got TypeSpec as parent of Member");
                //((TypeSpec)paren).AddMethod(buff,this);
            }
            if (paren is ReferenceScope)
                parent = ((ReferenceScope)paren).GetDefaultClass();
            parent = (Class)paren;
            //if ((MethodRef)parent.GetMethodDesc(name) != null) throw new PEFileException("Existing method!!");
            //sig = buff.ReadMethSig(this,name,sigIx);
            //MethodRef existing = (MethodRef)parent.GetMethod(sig);
            //if (existing != null) 
            //  return existing;
            parent.AddToMethodList(this);
            return this;
        }

        public void MakeVarArgMethod(MethodDef paren, Type[] optPars)
        {
            if (paren != null)
            {
                parent = null;
                varArgParent = paren;
            }
            sig.optParTypes = optPars;
            if (sig.optParTypes != null) sig.numOptPars = (uint)sig.optParTypes.Length;
            sig.callConv = CallConv.Vararg;
        }

        internal void MakeGenericPars(uint num)
        {
            // Experimental (kjg) 2007-09-03
            // It appears that for some system dll the MethodRef may not
            // have any generic params defined, but the methodSig does.
            if (genericParams == null)
                if (genericParams == null) genericParams = new ArrayList();
            for (int i = genericParams.Count; i < num; i++)
            {
                genericParams.Add(new GenericParam("GPar" + i, this, i));
            }
            // Previous code ...
            //if (genericParams != null) {
            //    for (int i=genericParams.Count; i < num; i++) {
            //        genericParams.Add(new GenericParam("GPar"+i,this,i));
            //    }
            //}
            //sig.numGenPars = (uint)genericParams.Count;      
        }

        /*------------------------- public set and get methods --------------------------*/

        /// <summary>
        /// Set the parameter types for this method
        /// </summary>
        /// <param name="pars">List of types of method parameters</param>
        public void SetParTypes(Type[] pars)
        {
            if (pars == null)
            {
                sig.numPars = 0;
                return;
            }
            sig.parTypes = pars;
            sig.numPars = (uint)pars.Length;
        }

        /// <summary>
        /// Set the list of optional parameter types for this method
        /// </summary>
        /// <param name="pars">list of optional parameter types</param>
        public void SetOptParTypes(Type[] pars)
        {
            if (pars == null)
            {
                sig.numOptPars = 0;
                return;
            }
            sig.optParTypes = pars;
            sig.numOptPars = (uint)sig.optParTypes.Length;
        }

        /*------------------------- internal functions --------------------------*/

        internal sealed override void TypeSig(MemoryStream sigStream)
        {
            sig.TypeSig(sigStream);
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.MemberRef, this);
            nameIx = md.AddToStringsHeap(name);
            if (parent != null)
            {
                if (parent is ClassSpec) md.AddToTable(MDTable.TypeSpec, parent);
                if (parent is ConstructedTypeSpec) 
                    md.AddToTable(MDTable.TypeSpec, ((ConstructedTypeSpec)parent).Spec);
                parent.BuildMDTables(md);
            }
            sig.BuildTables(md);
        }

        internal sealed override void BuildSignatures(MetaDataOut md)
        {
            sig.BuildSignatures(md);
            MemoryStream sigStream = new MemoryStream();
            TypeSig(sigStream);
            sigIx = md.AddToBlobHeap(sigStream.ToArray());
            done = false;
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
            if (varArgParent != null)
                output.WriteCodedIndex(CIx.MemberRefParent, varArgParent);
            else if (parent is ConstructedTypeSpec)
                output.WriteCodedIndex(CIx.MemberRefParent, ((ConstructedTypeSpec)parent).Spec);
            else
                output.WriteCodedIndex(CIx.MemberRefParent, parent);
            output.StringsIndex(nameIx);
            output.BlobIndex(sigIx);
        }

        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.HasCustomAttr): return 6;
                case (CIx.MethodDefOrRef): return 1;
                case (CIx.CustomAttributeType): return 3;
            }
            return 0;
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a method defined in THIS assembly/module
    /// IL     .method
    /// </summary>
    public class MethodDef : Method
    {
        private static readonly ushort PInvokeImpl = 0x2000;
        private static readonly ushort NotPInvoke = 0xDFFF;
        private static readonly ushort HasSecurity = 0x4000;
        private static readonly ushort NoSecurity = 0xBFFF;
        //private static readonly uint UnmanagedExport = 0x0008;
        uint parIx = 0, textOffset = 0;
        internal MethodRef refOf;

        // The default max stack depth to be assigned when the depth can not be calculated.
        private static readonly int DefaultMaxStackDepth = 8;

        CILInstructions code;
        uint rva;
        Param[] parList;
        Local[] locals;
        bool initLocals;
        ushort methFlags = 0, implFlags = 0;
        int maxStack = 0, numLocals = 0;
        uint numPars = 0;
        bool entryPoint = false;
        internal LocalSig localSig;
        MethodRef varArgSig;
        ImplMap pinvokeImpl;
        ArrayList security = null;
        internal uint locToken = 0;

        /*-------------------- Constructors ---------------------------------*/

        internal MethodDef(string name, Type retType, Param[] pars, ClassDef paren)
            : base(name, retType, paren)
        {
            sig.SetParTypes(pars);
            parList = pars;
            parent = paren;
            tabIx = MDTable.Method;
        }

        internal MethodDef(ClassDef paren, MethSig mSig, Param[] pars)
            : base(mSig.name)
        {
            sig = mSig;
            parList = pars;
            parent = paren;
            tabIx = MDTable.Method;
        }

        internal MethodDef(ClassSpec paren, MethSig mSig, Param[] pars)
            : base(mSig.name)
        {
            parent = paren;
            parList = pars;
            sig = mSig;
            tabIx = MDTable.Method;
        }

        internal MethodDef(PEReader buff)
            : base(null)
        {
            rva = buff.ReadUInt32();
            implFlags = buff.ReadUInt16();
            methFlags = buff.ReadUInt16();
            name = buff.GetString();
            sigIx = buff.GetBlobIx();
            parIx = buff.GetIndex(MDTable.Param);
            tabIx = MDTable.Method;
        }

        internal static void Read(PEReader buff, TableRow[] methDefs)
        {
            MethodDef prevDef = null;
            prevDef = new MethodDef(buff);
            methDefs[0] = prevDef;
            for (int i = 1; i < methDefs.Length; i++)
            {
                prevDef.Row = (uint)i;
                MethodDef methDef = new MethodDef(buff);
                prevDef.numPars = methDef.parIx - prevDef.parIx;
                prevDef = methDef;
                methDefs[i] = methDef;
            }
            prevDef.Row = (uint)methDefs.Length;
            prevDef.numPars = (buff.GetTableSize(MDTable.Param) + 1) - prevDef.parIx;
        }

        internal static void GetMethodRefs(PEReader buff, uint num, ClassRef parent)
        {
            for (int i = 0; i < num; i++)
            {
                uint rva = buff.ReadUInt32();
                ushort implFlags = buff.ReadUInt16();
                ushort methFlags = buff.ReadUInt16();
                string name = buff.GetString();
                uint sigIx = buff.GetBlobIx();
                uint parIx = buff.GetIndex(MDTable.Param);
                if (IsPublicOrProtected(methFlags))
                {
                    MethodRef mRef = new MethodRef(parIx, name, sigIx);  // changed
                    mRef.SetParent(parent);
                    //Console.WriteLine(parent.NameString());
                    MethSig mSig = buff.ReadMethSig(mRef, name, sigIx);
                    //mSig.name = name;
                    mRef.SetSig(mSig); // changed
                    parent.AddToMethodList(mRef);
                }
            }
        }

        private void DoPars(PEReader buff, bool resolvePars)
        {
            if (sig == null) sig = buff.ReadMethSig(this, sigIx);
            sig.name = name;
            parList = new Param[sig.numPars];
            if (parIx > buff.GetTableSize(MDTable.Param))
            {
                // EXPERIMENTAL kjg 19 November 2007
                //  It is actually allowed that a method def does not
                //  have corresponding Param metadata, provided the
                //  parameter types may be constructed from the sig.
                for (uint i = 0; i < sig.numPars; i++)
                {
                    parList[i] = Param.DefaultParam();
                    parList[i].SetParType(sig.parTypes[i]);
                }
            }
            else
            {
                for (uint i = 0; i < sig.numPars; i++)
                {
                    parList[i] = (Param)buff.GetElement(MDTable.Param, i + parIx);
                    if (resolvePars) parList[i].Resolve(buff, i + parIx, sig.parTypes[i]);
                    else parList[i].SetParType(sig.parTypes[i]);
                }
            }
        }

        private void DoCode(PEReader buff)
        {
            if (rva != 0)
            {
                if (Diag.DiagOn) Console.WriteLine("Reading byte codes for method " + name);
                buff.ReadByteCodes(this, rva);
            }
        }

        internal sealed override void Resolve(PEReader buff)
        {
            buff.currentMethodScope = this;
            buff.currentClassScope = parent;
            DoPars(buff, true);
            if (!buff.skipBody)
            {
                DoCode(buff);
            }
            buff.currentMethodScope = null;
            buff.currentClassScope = null;
        }

        /*------------------------- public set and get methods --------------------------*/

        /// <summary>
        /// Get the parameters of this method
        /// </summary>
        /// <returns>Array of params of this method</returns>
        public Param[] GetParams()
        {
            return parList;
        }

        /// <summary>
        /// Set the parameters for this method
        /// </summary>
        /// <param name="pars">Descriptors of the parameters for this method</param>
        public void SetParams(Param[] pars)
        {
            parList = pars;
            sig.SetParTypes(pars);
        }

        /// <summary>
        /// Add some attributes to this method descriptor
        /// </summary>
        /// <param name="ma">the attributes to be added</param>
        public void AddMethAttribute(MethAttr ma) { methFlags |= (ushort)ma; }

        /// <summary>
        /// Property to get and set the attributes for this method
        /// </summary>
        public MethAttr GetMethAttributes() { return (MethAttr)methFlags; }
        public void SetMethAttributes(MethAttr ma) { methFlags = (ushort)ma; }

        /// <summary>
        /// Add some implementation attributes to this method descriptor
        /// </summary>
        /// <param name="ia">the attributes to be added</param>
        public void AddImplAttribute(ImplAttr ia)
        {
            implFlags |= (ushort)ia;
        }

        /// <summary>
        /// Property to get and set the implementation attributes for this method
        /// </summary>
        public ImplAttr GetImplAttributes() { return (ImplAttr)implFlags; }
        public void SetImplAttributes(ImplAttr ia) { implFlags = (ushort)ia; }

        public void AddPInvokeInfo(ModuleRef scope, string methName,
            PInvokeAttr callAttr)
        {
            pinvokeImpl = new ImplMap((ushort)callAttr, this, methName, scope);
            methFlags |= PInvokeImpl;
        }

        public void RemovePInvokeInfo()
        {
            pinvokeImpl = null;
            methFlags &= NotPInvoke;
        }

        public void AddSecurity(SecurityAction act, byte[] permissionSet)
        {
            methFlags |= HasSecurity;
            if (security == null) security = new ArrayList();
            security.Add(new DeclSecurity(this, act, permissionSet));
        }

        public void AddSecurity(DeclSecurity sec)
        {
            methFlags |= HasSecurity;
            if (security == null) security = new ArrayList();
            security.Add(sec);
        }

        public DeclSecurity[] GetSecurity()
        {
            if (security == null) return null;
            return (DeclSecurity[])security.ToArray(typeof(DeclSecurity));
        }

        public void RemoveSecurity()
        {
            security = null;
            methFlags &= NoSecurity;
        }

        /// <summary>
        /// Set the maximum stack height for this method
        /// </summary>
        /// <param name="maxStack">the maximum height of the stack</param>
        public void SetMaxStack(int maxStack)
        {
            this.maxStack = maxStack;
        }

        /// <summary>
        /// Retrieve the maximum size of the stack for the code
        /// of this method
        /// </summary>
        /// <returns>max stack height for CIL codes</returns>
        public int GetMaxStack()
        {
            return maxStack;
        }

        /// <summary>
        /// Add local variables to this method
        /// </summary>
        /// <param name="locals">the locals to be added</param>
        /// <param name="initLocals">are locals initialised to default values</param>
        public void AddLocals(Local[] locals, bool initLocals)
        {
            if (locals == null) return;
            this.locals = locals;
            this.initLocals = initLocals;
            numLocals = locals.Length;
            for (int i = 0; i < numLocals; i++)
            {
                this.locals[i].SetIndex(i);
            }
        }

        /// <summary>
        /// Retrieve the locals for this method
        /// </summary>
        /// <returns>list of locals declared in this method</returns>
        public Local[] GetLocals() { return locals; }

        /// <summary>
        /// Remove all the locals from this method
        /// </summary>
        public void RemoveLocals()
        {
            locals = null;
            numLocals = 0;
            initLocals = false;
        }

        /// <summary>
        /// Mark this method as having an entry point
        /// </summary>
        public void DeclareEntryPoint() { entryPoint = true; }

        /// <summary>
        /// Does this method have an entrypoint?
        /// </summary>
        public bool HasEntryPoint() { return entryPoint; }

        /// <summary>
        /// Remove the entry point from this method
        /// </summary>
        public void RemoveEntryPoint() { entryPoint = false; }

        /// <summary>
        /// Create a code buffer for this method to add the IL instructions to
        /// </summary>
        /// <returns>a buffer for this method's IL instructions</returns>
        public CILInstructions CreateCodeBuffer()
        {
            code = new CILInstructions(this);
            return code;
        }

        /// <summary>
        /// Get the CIL code buffer for this method
        /// </summary>
        /// <returns>Code buffer for this method</returns>
        public CILInstructions GetCodeBuffer() { return code; }

        /// <summary>
        /// Make a method reference descriptor for this method to be used 
        /// as a callsite signature for this vararg method
        /// </summary>
        /// <param name="optPars">the optional pars for the vararg method call</param>
        /// <returns></returns>
        public MethodRef MakeVarArgSignature(Type[] optPars)
        {
            MethSig mSig = new MethSig(name);
            mSig.parTypes = sig.parTypes;
            mSig.retType = sig.retType;
            varArgSig = new MethodRef(sig);
            varArgSig.MakeVarArgMethod(this, optPars);
            return varArgSig;
        }

        public MethodRef GetVarArgSignature()
        {
            return varArgSig;
        }

        /// <summary>
        /// Get the MethodRef equivalent to this MethodDef.  Assumes 
        /// that one has been created.
        /// </summary>
        /// <returns>MethodRef for this MethodDef</returns>
        public MethodRef RefOf() { return refOf; }

        /// <summary>
        /// Get the MethodRef equivalent to this MethodDef.  If one
        /// does not exist, then create it.
        /// </summary>
        /// <returns>MethodRef for this MethodDef</returns>
        public MethodRef MakeRefOf()
        {
            if (refOf != null) return refOf;
            ClassRef parRef = ((ClassDef)parent).MakeRefOf();
            refOf = parRef.GetMethod(name, sig.parTypes);
            if (refOf == null)
            {
                Type rType = sig.MakeRefRetType();
                Type[] pTypes = sig.MakeRefParTypes();
                refOf = new MethodRef(parRef, name, rType, pTypes);
                refOf.defOf = this;
                refOf.AddCallConv(this.GetCallConv());
            }
            return refOf;
        }

        /*------------------------- internal functions --------------------------*/

        private static bool IsPublicOrProtected(ushort methFlags)
        {
            return (methFlags & (ushort)MethAttr.Public) == (ushort)MethAttr.Public ||
                   (methFlags & (ushort)MethAttr.Family) == (ushort)MethAttr.Family;
        }

        internal void InsertGenericParam(GenericParam genPar)
        {
            if (genericParams == null) genericParams = new ArrayList();
            for (int i = 0; i < genericParams.Count - genPar.Index; i++)
            {
                genericParams.Add(null);
            }
            genericParams.Insert((int)genPar.Index, genPar);
        }

        internal override bool isDef() { return true; }

        internal PEFile GetScope()
        {
            return ((ClassDef)parent).GetScope();
        }

        internal void ChangeRefsToDefs(ClassDef newPar, ClassDef[] oldTypes)
        {
            parent = newPar;
            sig.ChangeParTypes(newPar, oldTypes);
            if (code != null)
                code.ChangeRefsToDefs(newPar, oldTypes);
        }

        internal void AddPInvokeInfo(ImplMap impl)
        {
            pinvokeImpl = impl;
            methFlags |= PInvokeImpl;
        }

        internal void AddVarArgSig(MethodRef meth)
        {
            varArgSig = meth;
            //meth.MakeVarArgMethod(this,null);
        }

        internal sealed override void TypeSig(MemoryStream sigStream)
        {
            sig.TypeSig(sigStream);
        }

        // fix for Whidbey bug
        internal void AddGenericsToTable(MetaDataOut md)
        {
            if (genericParams != null)
            {
                for (int i = 0; i < genericParams.Count; i++)
                {
                    md.AddToTable(MDTable.GenericParam, (GenericParam)genericParams[i]);
                }
            }
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.Method, this);
            nameIx = md.AddToStringsHeap(name);
            if (genericParams != null)
            {
                for (int i = 0; i < genericParams.Count; i++)
                {
                    ((GenericParam)genericParams[i]).BuildMDTables(md);
                }
            }
            if (security != null)
            {
                for (int i = 0; i < security.Count; i++)
                {
                    ((DeclSecurity)security[i]).BuildMDTables(md);
                }
            }
            if (pinvokeImpl != null) pinvokeImpl.BuildMDTables(md);
            if (entryPoint) md.SetEntryPoint(this);
            if (locals != null)
            {
                localSig = new LocalSig(locals);
                localSig.BuildMDTables(md);
            }
            try
            {
                if (code != null)
                {
                    if (code.IsEmpty())
                    {
                        code = null;
                    }
                    else
                    {
                        code.BuildTables(md);
                    }
                }
            }
            catch (InstructionException ex)
            {
                throw new Exception(ex.AddMethodName(name));
            }
            parIx = md.TableIndex(MDTable.Param);
            for (int i = 0; i < sig.numPars; i++)
            {
                parList[i].seqNo = (ushort)(i + 1);
                parList[i].BuildMDTables(md);
            }
            sig.BuildTables(md);
        }

        internal sealed override void BuildCILInfo(CILWriter output)
        {
            if (genericParams != null)
            {
                for (int i = 0; i < genericParams.Count; i++)
                {
                    ((GenericParam)genericParams[i]).BuildCILInfo(output);
                }
            }
            if (security != null)
            {
                for (int i = 0; i < security.Count; i++)
                {
                    ((DeclSecurity)security[i]).BuildCILInfo(output);
                }
            }
            if (pinvokeImpl != null) pinvokeImpl.BuildCILInfo(output);
            if (locals != null)
            {
                for (int i = 0; i < locals.Length; i++)
                {
                    locals[i].BuildCILInfo(output);
                }
            }
            try
            {
                if (code != null) code.BuildCILInfo(output);
            }
            catch (InstructionException ex)
            {
                throw new Exception(ex.AddMethodName(name));
            }
            sig.BuildCILInfo(output);

        }

        internal sealed override void BuildSignatures(MetaDataOut md)
        {
            if (locals != null)
            {
                localSig.BuildSignatures(md);
                locToken = localSig.Token();
            }
            if (code != null)
            {
                // If the stack depth has not been explicity set, try to work out what is needed.
                if (maxStack == 0)
                {
                    try
                    {

                        // Set the flag to show if the return type is void or other.
                        code.ReturnsVoid = GetRetType().SameType(PrimitiveType.Void);

                        // Calculate the max stack depth
                        maxStack = code.GetMaxStackDepthRequired();
                    }
                    catch (CouldNotFindMaxStackDepth)
                    {
                        // Could not find the depth, assign the default
                        maxStack = DefaultMaxStackDepth;
                    }
                }
                code.CheckCode(locToken, initLocals, maxStack, md);
                textOffset = md.AddCode(code);
                if (Diag.DiagOn) Console.WriteLine("code offset = " + textOffset);
            }
            sig.BuildSignatures(md);
            MemoryStream outSig = new MemoryStream();
            TypeSig(outSig);
            sigIx = md.AddToBlobHeap(outSig.ToArray());
            done = false;
        }

        internal static uint Size(MetaData md)
        {
            return 8 + md.StringsIndexSize() + md.BlobIndexSize() + md.TableIndexSize(MDTable.Param);
        }

        internal sealed override void Write(PEWriter output)
        {
            if (code == null) output.Write(0);
            else output.WriteCodeRVA(textOffset);
            output.Write(implFlags);
            output.Write(methFlags);
            output.StringsIndex(nameIx);
            output.BlobIndex(sigIx);
            output.WriteIndex(MDTable.Param, parIx);
        }

        internal override void Write(CILWriter output)
        {
            output.Write("  .method ");
            WriteFlags(output, methFlags);
            sig.Write(output);
            output.Write(" " + name + "(");
            if (parList != null)
            {
                for (int i = 0; i < parList.Length; i++)
                {
                    parList[i].Write(output);
                    if (i < parList.Length - 1)
                    {
                        output.Write(", ");
                    }
                }
            }
            output.Write(") ");
            uint codeType = implFlags & (uint)0x11;
            if (codeType == 0)
            {
                output.Write("cil ");
            }
            else if (codeType == 1)
            {
                output.Write("native ");
            }
            else if (codeType == 3)
            {
                output.Write("runtime ");
            }
            if ((implFlags & (uint)ImplAttr.Unmanaged) == 0)
            {
                output.Write("managed ");
            }
            else
            {
                output.Write("unmanaged ");
            }
            if ((implFlags & (uint)ImplAttr.ForwardRef) != 0)
            {
                output.Write("forwardref ");
            }
            if ((implFlags & (uint)ImplAttr.InternalCall) != 0)
            {
                output.Write("internalcall ");
            }
            if ((implFlags & (uint)ImplAttr.Synchronized) != 0)
            {
                output.Write("synchronized ");
            }
            if ((implFlags & (uint)ImplAttr.NoInLining) != 0)
            {
                output.Write("noinlining ");
            }
            output.WriteLine(" {");
            if ((locals != null) && (locals.Length > 0))
            {
                output.Write("      .locals (");
                for (int i = 0; i < locals.Length; i++)
                {
                    if (i > 0)
                    {
                        output.Write("              ");
                    }
                    locals[i].Write(output);
                    if (i < locals.Length - 1)
                    {
                        output.WriteLine(",");
                    }
                }
                output.WriteLine(" )");
            }
            if (entryPoint)
            {
                output.WriteLine("      .entrypoint");
            }
            if (code != null) code.Write(output);
            output.WriteLine("  }");
        }


        internal sealed override uint GetCodedIx(CIx code)
        {
            switch (code)
            {
                case (CIx.HasCustomAttr): return 0;
                case (CIx.HasDeclSecurity): return 1;
                case (CIx.MemberRefParent): return 3;
                case (CIx.MethodDefOrRef): return 0;
                case (CIx.MemberForwarded): return 1;
                case (CIx.CustomAttributeType): return 2;
                case (CIx.TypeOrMethodDef): return 1;
            }
            return 0;
        }

    }

}