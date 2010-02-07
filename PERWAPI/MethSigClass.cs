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


namespace QUT.PERWAPI
{
    /**************************************************************************/
    public class MethSig
    {
        internal string name;
        internal CallConv callConv = CallConv.Default;
        internal Type retType;
        internal Type[] parTypes, optParTypes;
        internal uint numPars = 0, numOptPars = 0, numGenPars = 0;
        //uint sigIx;

        /*-------------------- Constructors ---------------------------------*/

        internal MethSig(string nam)
        {
            name = nam;
        }

        internal MethSig InstantiateGenTypes(Class classType, Type[] genTypes)
        {
            MethSig newSig = new MethSig(name);
            newSig.callConv = callConv;
            newSig.numPars = numPars;
            newSig.numOptPars = numOptPars;
            newSig.numGenPars = numGenPars;
            newSig.parTypes = ReplaceGenPars(parTypes, classType, genTypes);
            newSig.optParTypes = ReplaceGenPars(optParTypes, classType, genTypes);
            newSig.retType = SubstituteType(retType, classType, genTypes);
            return newSig;
        }

        private Type[] ReplaceGenPars(Type[] typeList, MetaDataElement paren, Type[] genTypes)
        {
            if (typeList == null) return null;
            Type[] newList = new Type[typeList.Length];
            for (int i = 0; i < typeList.Length; i++)
            {
                newList[i] = SubstituteType(typeList[i], paren, genTypes);
            }
            return newList;
        }

        private Type SubstituteType(Type origType, MetaDataElement paren, Type[] genTypes)
        {
            if ((origType is GenericParam) && (((GenericParam)origType).GetParent() == paren))
                return genTypes[((GenericParam)origType).Index];
            return origType;
        }

        internal void SetParTypes(Param[] parList)
        {
            if (parList == null) { numPars = 0; return; }
            numPars = (uint)parList.Length;
            parTypes = new Type[numPars];
            for (int i = 0; i < numPars; i++)
            {
                parTypes[i] = parList[i].GetParType();
            }
        }

        internal void ChangeParTypes(ClassDef newType, ClassDef[] oldTypes)
        {
            System.Diagnostics.Debug.Assert(newType != null);
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

        internal Type MakeRefRetType()
        {
            if (retType is ClassDef)
            {
                return ((ClassDef)retType).MakeRefOf();
            }
            else
            {
                return retType;
            }
        }

        internal Type[] MakeRefParTypes()
        {
            Type[] pTypes = new Type[numPars];
            for (int i = 0; i < numPars; i++)
            {
                if (parTypes[i] is ClassDef)
                {
                    pTypes[i] = ((ClassDef)parTypes[i]).MakeRefOf();
                }
                else
                {
                    pTypes[i] = parTypes[i];
                }
            }
            return pTypes;
        }

        /*
         *     internal bool HasNameAndSig(string name, Type[] sigTypes) {
              if (this.name.CompareTo(name) != 0) return false;
              return HasSig(sigTypes);
            }

            internal bool HasNameAndSig(string name, Type[] sigTypes, Type[] optTypes) {
              if (this.name.CompareTo(name) != 0) return false;
              return HasSig(sigTypes,optTypes);
            }
            */

        internal bool HasSig(Type[] sigTypes)
        {
            if (sigTypes == null) return (numPars == 0);
            if (sigTypes.Length != numPars) return false;
            for (int i = 0; i < numPars; i++)
            {
                if (!sigTypes[i].SameType(parTypes[i]))
                    return false;
            }
            return (optParTypes == null) || (optParTypes.Length == 0);
        }

        internal bool HasSig(Type[] sigTypes, Type[] optTypes)
        {
            if (sigTypes == null)
            {
                if (numPars > 0) return false;
                if (optTypes == null) return (numOptPars == 0);
            }
            if (sigTypes.Length != numPars) return false;
            for (int i = 0; i < numPars; i++)
            {
                if (!sigTypes[i].SameType(parTypes[i]))
                    return false;
            }
            if (optTypes == null) return numOptPars == 0;
            if (optTypes.Length != numOptPars) return false;
            for (int i = 0; i < optTypes.Length; i++)
            {
                if (!optTypes[i].SameType(optParTypes[i]))
                    return false;
            }
            return true;
        }
        /*    
           internal void CheckParTypes(Param[] parList) {
             //numGenPars = 0;
             for (int i=0; i < numPars; i++) {
               if (parTypes[i] is GenericParam)
                 numGenPars++;
             }
             if (numGenPars > 0)
               callConv |= CallConv.Generic;
             else if ((callConv & CallConv.Generic) > 0)
               callConv ^= CallConv.Generic;
           }
           */

        internal void TypeSig(MemoryStream sig)
        {
            sig.WriteByte((byte)callConv);
            if (numGenPars > 0) 
                MetaDataOut.CompressNum(BlobUtil.CompressUInt(numGenPars), sig);
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
        }

        /// <summary>
        /// Check to see if the method signature has a particular calling convention.
        /// </summary>
        /// <param name="callCon">The convention to check to see if the method has.</param>
        /// <returns>Ture if the calling convention exists on the method.</returns>
        internal bool HasCallConv(CallConv callCon)
        {
            return ((callConv & callCon) == callCon);
        }

        internal void WriteCallConv(CILWriter output)
        {
            if ((callConv & CallConv.Instance) != 0)
            {
                output.Write("instance ");
                if ((callConv & CallConv.InstanceExplicit) == CallConv.InstanceExplicit)
                {
                    output.Write("explicit ");
                }
            }
            uint callKind = (uint)callConv & 0x07;
            switch (callKind)
            {
                case 0: break;
                case 1: output.Write("unmanaged cdecl "); break;
                case 2: output.Write("unmanaged stdcall "); break;
                case 3: output.Write("unmanaged thiscall "); break;
                case 4: output.Write("unmanaged fastcall "); break;
                case 5: output.Write("vararg "); break;
            }
        }

        internal void Write(CILWriter output)
        {
            WriteCallConv(output);
            retType.WriteType(output);
        }

        internal void WriteParTypes(CILWriter output)
        {
            output.Write("(");
            for (int i = 0; i < numPars; i++)
            {
                parTypes[i].WriteType(output);
                if ((i < numPars - 1) || (numOptPars > 0))
                    output.Write(", ");
            }
            for (int i = 0; i < numOptPars; i++)
            {
                optParTypes[i].WriteType(output);
                if (i < numPars - 1)
                    output.Write(", ");
            }
            output.Write(")");
        }

        internal string NameString()
        {
            string parString = "(";
            if (numPars > 0)
            {
                parString += parTypes[0].NameString();
                for (int i = 1; i < numPars; i++)
                {
                    parString += "," + parTypes[i].NameString();
                }
            }
            if (numOptPars > 0)
            {
                if (numPars > 0) parString += ",";
                parString += optParTypes[0].NameString();
                for (int i = 1; i < numOptPars; i++)
                {
                    parString += "," + optParTypes[i].NameString();
                }
            }
            return name + parString + ")";
        }

        internal void BuildTables(MetaDataOut md)
        {
            if (!retType.isDef())
                retType.BuildMDTables(md);
            for (int i = 0; i < numPars; i++)
            {
                if (!parTypes[i].isDef())
                    parTypes[i].BuildMDTables(md);
            }
            for (int i = 0; i < numOptPars; i++)
            {
                if (!optParTypes[i].isDef())
                    optParTypes[i].BuildMDTables(md);
            }
        }

        internal void BuildCILInfo(CILWriter output)
        {
            if (!retType.isDef())
                retType.BuildCILInfo(output);
            for (int i = 0; i < numPars; i++)
            {
                if (!parTypes[i].isDef())
                    parTypes[i].BuildCILInfo(output);
            }
            for (int i = 0; i < numOptPars; i++)
            {
                if (!optParTypes[i].isDef())
                    optParTypes[i].BuildCILInfo(output);
            }
        }

        internal void BuildSignatures(MetaDataOut md)
        {
            if (!retType.isDef())
                retType.BuildSignatures(md);
            for (int i = 0; i < numPars; i++)
            {
                if (!parTypes[i].isDef())
                    parTypes[i].BuildSignatures(md);
            }
            for (int i = 0; i < numOptPars; i++)
            {
                if (!optParTypes[i].isDef())
                    optParTypes[i].BuildSignatures(md);
            }
        }

    }


}