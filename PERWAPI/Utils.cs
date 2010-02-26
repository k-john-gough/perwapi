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

namespace QUT.PERWAPI
{

    /// <summary>
    /// Diagnostic
    /// </summary>
    public class Diag
    {
        /// <summary>
        /// Flag for diagnostic output.
        /// </summary>
        public static bool DiagOn = false;
    }

    public static class BlobUtil
    {
        public static byte[] CompressInt(int val)
        {
            // This code is based on a revised version of the
            // ECMA-335 spec which clarifies the encoding of 
            // array lower bounds. (kjg 2008-Feb-22 )
            //
            uint uVal = ((uint)val) << 1;
            uint sign = (val < 0 ? (uint)1 : 0);

            if (-64 <= val && val <= 63)
            {
                uVal = uVal & 0x7f | sign;
                return new byte[] { (byte)uVal };
            }
            else if (-8192 <= val && val <= 8191)
            {
                uVal = uVal & 0x3fff | 0x8000 | sign;
                return new byte[] { (byte)(uVal >> 8), (byte)uVal };
            }
            else if (-268435456 <= val && val <= 268435455)
            {
                uVal = uVal & 0x1fffffff | 0xc0000000 | sign;
                return new byte[] { 
                    (byte)(uVal >> 24), 
                    (byte)(uVal >> 16), 
                    (byte)(uVal >> 8), 
                    (byte)uVal };
            }
            else 
                throw new OverflowException("Value cannot be compressed");
        }

        public static byte[] CompressUInt(uint val)
        {
            if (val > 0x1fffffff)
                throw new OverflowException("Value cannot be compressed");
            return CompressNum(val);
        }

        private static byte[] CompressNum(uint num)
        {
            byte[] rslt = null;            
            if (num <= 0x7f)
            {
                rslt = new byte[1]; 
                rslt[0] = (byte)num;
            }
            else if (num <= 0x3fff)
            {
                rslt = new byte[2];
                rslt[0] = (byte)((num >> 8) | 0x80);
                rslt[1] = (byte)(num & 0xff);
            }
            else
            {
                rslt = new byte[4];
                rslt[0] = (byte)((num >> 24) | 0xc0);
                rslt[1] = (byte)((num >> 16) & 0xff);
                rslt[2] = (byte)((num >> 8) & 0xff);
                rslt[3] = (byte)(num & 0xff);
            }
            return rslt;
        }
    }


    /// <summary>
    /// Facilities for outputting hexadecimal strings
    /// </summary>
    public class Hex
    {
        readonly static char[] hexDigit = {'0','1','2','3','4','5','6','7',
                                              '8','9','A','B','C','D','E','F'};
        readonly static uint[] iByteMask = { 0x000000FF, 0x0000FF00, 0x00FF0000, 0xFF000000 };
        readonly static ulong[] lByteMask = {0x00000000000000FF, 0x000000000000FF00, 
                                                0x0000000000FF0000, 0x00000000FF000000,
                                                0x000000FF00000000, 0x0000FF0000000000,
                                                0x00FF000000000000, 0xFF00000000000000 };
        readonly static uint nibble0Mask = 0x0000000F;
        readonly static uint nibble1Mask = 0x000000F0;

        /// <summary>
        /// Derives a hexadecimal string for a byte value
        /// </summary>
        /// <param name="b">the byte value</param>
        /// <returns>hex string for the byte value</returns>
        public static String Byte(int b)
        {
            char[] str = new char[2];
            uint num = (uint)b;
            uint b1 = num & nibble0Mask;
            uint b2 = (num & nibble1Mask) >> 4;
            str[0] = hexDigit[b2];
            str[1] = hexDigit[b1];
            return new String(str);
        }

        /// <summary>
        /// Derives a hexadecimal string for a short value
        /// </summary>
        /// <param name="b">the short value</param>
        /// <returns>hex string for the short value</returns>
        public static String Short(int b)
        {
            char[] str = new char[4];
            uint num1 = (uint)b & iByteMask[0];
            uint num2 = ((uint)b & iByteMask[1]) >> 8;
            uint b1 = num1 & nibble0Mask;
            uint b2 = (num1 & nibble1Mask) >> 4;
            uint b3 = num2 & nibble0Mask;
            uint b4 = (num2 & nibble1Mask) >> 4;
            str[0] = hexDigit[b4];
            str[1] = hexDigit[b3];
            str[2] = hexDigit[b2];
            str[3] = hexDigit[b1];
            return new String(str);
        }

        /// <summary>
        /// Derives a hexadecimal string for an int value
        /// </summary>
        /// <param name="val">the int value</param>
        /// <returns>hex string for the int value</returns>
        public static String Int(int val)
        {
            char[] str = new char[8];
            uint num = (uint)val;
            int strIx = 7;
            for (int i = 0; i < iByteMask.Length; i++)
            {
                uint b = num & iByteMask[i];
                b >>= (i * 8);
                uint b1 = b & nibble0Mask;
                uint b2 = (b & nibble1Mask) >> 4;
                str[strIx--] = hexDigit[b1];
                str[strIx--] = hexDigit[b2];
            }
            return new String(str);
        }

        /// <summary>
        /// Derives a hexadecimal string for an unsigned int value
        /// </summary>
        /// <param name="num">the unsigned int value</param>
        /// <returns>hex string for the unsigned int value</returns>
        public static String Int(uint num)
        {
            char[] str = new char[8];
            int strIx = 7;
            for (int i = 0; i < iByteMask.Length; i++)
            {
                uint b = num & iByteMask[i];
                b >>= (i * 8);
                uint b1 = b & nibble0Mask;
                uint b2 = (b & nibble1Mask) >> 4;
                str[strIx--] = hexDigit[b1];
                str[strIx--] = hexDigit[b2];
            }
            return new String(str);
        }

        /// <summary>
        /// Derives a hexadecimal string for a long value
        /// </summary>
        /// <param name="lnum">the long value</param>
        /// <returns>hex string for the long value</returns>
        public static String Long(long lnum)
        {
            ulong num = (ulong)lnum;
            return Long(num);
        }

        /// <summary>
        /// Derives a hexadecimal string for an unsigned long value
        /// </summary>
        /// <param name="num">the unsigned long value</param>
        /// <returns>hex string for the unsigned long value</returns>
        public static String Long(ulong num)
        {
            char[] str = new char[16];
            int strIx = 15;
            for (int i = 0; i < lByteMask.Length; i++)
            {
                ulong b = num & lByteMask[i];
                b >>= (i * 8);
                ulong b1 = b & nibble0Mask;
                ulong b2 = (b & nibble1Mask) >> 4;
                str[strIx--] = hexDigit[b1];
                str[strIx--] = hexDigit[b2];
            }
            return new String(str);
        }

    }

    /// <summary>
    /// Exception for features yet to be implemented
    /// </summary>
    public class NotYetImplementedException : System.Exception
    {
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="msg"></param>
        public NotYetImplementedException(string msg) : base(msg + " Not Yet Implemented") { }
    }

    /// <summary>
    /// Error in a type signature
    /// </summary>
    public class TypeSignatureException : System.Exception
    {
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="msg"></param>
        public TypeSignatureException(string msg) : base(msg) { }
    }

    /// <summary>
    /// Error with a CIL instruction
    /// </summary>
    public class InstructionException : System.Exception
    {
        IType iType;
        uint op;

        internal InstructionException(IType iType, uint op)
        {
            this.iType = iType;
            this.op = op;
        }

        internal string AddMethodName(string name)
        {
            string istr = " ";
            switch (iType)
            {
                case (IType.fieldOp): istr += (FieldOp)op; break;
                case (IType.methOp): istr += (MethodOp)op; break;
                case (IType.specialOp): istr += (SpecialOp)op; break;
                case (IType.typeOp): istr += (TypeOp)op; break;
                default: break;
            }
            return "NullPointer in instruction" + istr + " for method " + name;
        }

    }

    /// <summary>
    /// Error with descriptor types
    /// </summary>
    public class DescriptorException : System.Exception
    {

        /// <summary>
        /// exception
        /// </summary>
        /// <param name="msg"></param>
        public DescriptorException(string msg)
            :
            base("Descriptor for " + msg + " already exists") { }

        /// <summary>
        /// exception
        /// </summary>
        public DescriptorException()
            :
            base("Descriptor is a Def when a Ref is required") { }

    }

    /// <summary>
    /// Error for invalid PE file
    /// </summary>
    public class PEFileException : System.Exception
    {
        /// <summary>
        /// PEFile exception constructor
        /// </summary>
        /// <param name="msg"></param>
        public PEFileException(string msg) : base("Error in PE File:  " + msg) { }
    }

    /// <summary>
    /// When the maximum stack depth could not be found dynamically.
    /// </summary>
    public class CouldNotFindMaxStackDepth : System.Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public CouldNotFindMaxStackDepth() : base("Not able to find the maximum stack depth.") { }
    }

    /// <summary>
    /// When the stack depth is not valid for the current position.
    /// </summary>
    public class InvalidStackDepth : System.Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public InvalidStackDepth(string msg) : base("Invalid stack depth reached: " + msg) { }
    }

}