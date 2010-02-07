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
    /// Base descriptor for signature blobs
    /// </summary>
    public class Signature : MetaDataElement
    {
        protected uint sigIx;
        protected byte[] sigBytes;

        /*-------------------- Constructors ---------------------------------*/

        internal Signature()
        {
            tabIx = MDTable.StandAloneSig;
        }

        private Signature(uint sIx)
        {
            sigIx = sIx;
        }

        internal static void Read(PEReader buff, TableRow[] sigs)
        {
            for (int i = 0; i < sigs.Length; i++)
            {
                uint sigIx = buff.GetBlobIx();
                uint tag = buff.FirstBlobByte(sigIx);
                if (tag == LocalSig.LocalSigByte)
                    sigs[i] = new LocalSig(sigIx);
                else if (tag == Field.FieldTag)
                    sigs[i] = new Signature(sigIx);
                else
                    sigs[i] = new CalliSig(sigIx);
                sigs[i].Row = (uint)i + 1;
            }
        }

        internal override void Resolve(PEReader buff)
        {
            Type sigType = buff.GetFieldType(sigIx);
            buff.ReplaceSig(this, sigType);
        }

        internal static uint Size(MetaData md)
        {
            return md.BlobIndexSize();
        }

        internal sealed override void Write(PEWriter output)
        {
            output.BlobIndex(sigIx);
        }

        internal sealed override uint GetCodedIx(CIx code) { return (uint)tabIx; }

    }

    /**************************************************************************/
    /// <summary>
    /// Signature for calli instruction
    /// </summary>
    public class CalliSig : Signature
    {
        CallConv callConv;
        Type retType;
        Type[] parTypes, optParTypes;
        uint numPars = 0, numOptPars = 0;

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create a signature for a calli instruction
        /// </summary>
        /// <param name="cconv">calling conventions</param>
        /// <param name="retType">return type</param>
        /// <param name="pars">parameter types</param>
        public CalliSig(CallConv cconv, Type retType, Type[] pars)
        {
            callConv = cconv;
            this.retType = retType;
            parTypes = pars;
            if (pars != null) numPars = (uint)pars.Length;
        }

        internal CalliSig(uint sigIx)
        {
            this.sigIx = sigIx;
        }

        /// <summary>
        /// The return type of the method being called.
        /// </summary>
        public Type ReturnType { get { return retType; } }

        /// <summary>
        /// The number of parameters on the method being called.
        /// </summary>
        public uint NumPars { get { return numPars; } }

        /// <summary>
        /// The number of optional parameters on the method being called.
        /// </summary>
        public uint NumOptPars { get { return numOptPars; } }

        /// <summary>
        /// Check to see if the method signature has a particular calling convention.
        /// </summary>
        /// <param name="callCon">The convention to check to see if the method has.</param>
        /// <returns>Ture if the calling convention exists on the method.</returns>
        internal bool HasCallConv(CallConv callCon)
        {
            return ((callConv & callCon) == callCon);
        }

        internal sealed override void Resolve(PEReader buff)
        {
            MethSig mSig = buff.ReadMethSig(null, sigIx);
            callConv = mSig.callConv;
            retType = mSig.retType;
            parTypes = mSig.parTypes;
            if (parTypes != null) numPars = (uint)parTypes.Length;
            optParTypes = mSig.optParTypes;
            if (optParTypes != null) numOptPars = (uint)optParTypes.Length;
        }

        /// <summary>
        /// Add the optional parameters to a vararg method
        /// This method sets the vararg calling convention
        /// </summary>
        /// <param name="optPars">the optional pars for the vararg call</param>
        public void AddVarArgs(Type[] optPars)
        {
            optParTypes = optPars;
            if (optPars != null) numOptPars = (uint)optPars.Length;
            callConv |= CallConv.Vararg;
        }

        /// <summary>
        /// Add extra calling conventions to this callsite signature
        /// </summary>
        /// <param name="cconv"></param>
        public void AddCallingConv(CallConv cconv)
        {
            callConv |= cconv;
        }

        internal void ChangeRefsToDefs(ClassDef newType, ClassDef[] oldTypes)
        {
            for (int i = 0; i < oldTypes.Length; i++)
            {
                if (retType == oldTypes[i]) retType = newType;
                for (int j = 0; j < numPars; j++)
                {
                    if (parTypes[j] == oldTypes[i])
                        parTypes[j] = newType;
                }
                for (int j = 0; j < numOptPars; j++)
                {
                    if (optParTypes[j] == oldTypes[i])
                        optParTypes[j] = newType;
                }
            }
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(MDTable.StandAloneSig, this);
            for (int i = 0; i < numPars; i++)
            {
                parTypes[i].BuildMDTables(md);
            }
            if (numOptPars > 0)
            {
                for (int i = 0; i < numOptPars; i++)
                {
                    optParTypes[i].BuildMDTables(md);
                }
            }
        }

        internal sealed override void BuildSignatures(MetaDataOut md)
        {
            MemoryStream sig = new MemoryStream();
            sig.WriteByte((byte)callConv);
            MetaDataOut.CompressNum(BlobUtil.CompressUInt(numPars + numOptPars), sig);
            retType.TypeSig(sig);
            for (int i = 0; i < numPars; i++)
            {
                parTypes[i].TypeSig(sig);
            }
            if (numOptPars > 0)
            {
                sig.WriteByte((byte)ElementType.Sentinel);
                for (int i = 0; i < numOptPars; i++)
                {
                    optParTypes[i].TypeSig(sig);
                }
            }
            sigIx = md.AddToBlobHeap(sig.ToArray());
            done = false;
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for the locals for a method
    /// </summary>
    public class LocalSig : Signature
    {
        internal static readonly byte LocalSigByte = 0x7;
        Local[] locals;
        bool resolved = true;

        /*-------------------- Constructors ---------------------------------*/

        public LocalSig(Local[] locals)
        {
            this.locals = locals;
        }

        internal LocalSig(uint sigIx)
        {
            resolved = false;
            this.sigIx = sigIx;
        }

        internal override void Resolve(PEReader buff)
        {
        }

        internal void Resolve(PEReader buff, MethodDef meth)
        {
            if (resolved) return;
            buff.currentMethodScope = meth;
            buff.currentClassScope = (Class)meth.GetParent();
            locals = buff.ReadLocalSig(sigIx);
            buff.currentMethodScope = null;
            buff.currentClassScope = null;
        }

        internal Local[] GetLocals()
        {
            return locals;
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(tabIx, this);
            for (int i = 0; i < locals.Length; i++)
            {
                locals[i].BuildTables(md);
            }
        }

        internal byte[] SigBytes()
        {
            MemoryStream sig = new MemoryStream();
            sig.WriteByte(LocalSigByte);
            MetaDataOut.CompressNum(BlobUtil.CompressUInt((uint)locals.Length), sig);
            for (int i = 0; i < locals.Length; i++)
            {
                ((Local)locals[i]).TypeSig(sig);
            }
            return sig.ToArray();
        }

        internal sealed override void BuildSignatures(MetaDataOut md)
        {
            sigIx = md.AddToBlobHeap(SigBytes());
            done = false;
        }

    }

    /// <summary>
    /// Stores the signature for the debug info for a local variable.
    /// </summary>
    public class DebugLocalSig : Signature
    {
        internal static readonly byte LocalSigByte = 0x6;
        bool resolved = true;
        byte[] loc;

        /*-------------------- Constructors ---------------------------------*/

        internal DebugLocalSig(byte[] loc)
        {
            this.loc = loc;
        }

        internal DebugLocalSig(uint sigIx)
        {
            resolved = false;
            this.sigIx = sigIx;
        }

        internal override void Resolve(PEReader buff)
        {
        }

        internal void Resolve(PEReader buff, MethodDef meth)
        {
            if (resolved) return;
        }

        internal sealed override void BuildTables(MetaDataOut md)
        {
            md.AddToTable(tabIx, this);
        }

        internal byte[] SigBytes()
        {
            byte[] b = new byte[loc.Length + 1];
            b[0] = LocalSigByte;
            System.Array.Copy(loc, 0, b, 1, loc.Length);
            return b;
        }

        internal sealed override void BuildSignatures(MetaDataOut md)
        {
            sigIx = md.AddToBlobHeap(SigBytes());
            done = false;
        }

    }

}