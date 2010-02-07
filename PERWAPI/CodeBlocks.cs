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
using System.Collections;
using System.Text;

namespace QUT.PERWAPI
{
    /**************************************************************************/
    public abstract class CodeBlock
    {
        private static readonly int maxCodeSize = 255;
        protected CILLabel start, end;
        protected bool small = true;

        /*-------------------- Constructors ---------------------------------*/

        public CodeBlock(CILLabel start, CILLabel end)
        {
            this.start = start;
            this.end = end;
        }

        /// <summary>
        /// The label that marks the start of this code block
        /// </summary>
        public CILLabel Start { get { return start; } }

        /// <summary>
        /// The label that marks the end of this code block
        /// </summary>
        public CILLabel End { get { return end; } }

        internal virtual bool isFat()
        {
            // Console.WriteLine("block start = " + start.GetLabelOffset() +
            //                  "  block end = " + end.GetLabelOffset());
            return (end.GetLabelOffset() - start.GetLabelOffset()) > maxCodeSize;
        }

        internal virtual void Write(PEWriter output, bool fatFormat)
        {
            if (fatFormat) output.Write(start.GetLabelOffset());
            else output.Write((short)start.GetLabelOffset());
            uint len = end.GetLabelOffset() - start.GetLabelOffset();
            if (Diag.DiagOn) Console.WriteLine("block start = " + start.GetLabelOffset() + "  len = " + len);
            if (fatFormat) output.Write(len);
            else output.Write((byte)len);
        }

    }

    /**************************************************************************/
    /// <summary>
    /// The descriptor for a guarded block (.try)
    /// </summary>
    public class TryBlock : CodeBlock
    {
        protected bool fatFormat = false;
        protected ushort flags = 0;
        ArrayList handlers = new ArrayList();

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create a new try block
        /// </summary>
        /// <param name="start">start label for the try block</param>
        /// <param name="end">end label for the try block</param>
        public TryBlock(CILLabel start, CILLabel end) : base(start, end) { }

        /// <summary>
        /// Add a handler to this try block
        /// </summary>
        /// <param name="handler">a handler to be added to the try block</param>
        public void AddHandler(HandlerBlock handler)
        {
            //flags = handler.GetFlag();
            handlers.Add(handler);
        }

        /// <summary>
        /// Get an array containing all the handlers.
        /// </summary>
        /// <returns>The list of handlers.</returns>
        public HandlerBlock[] GetHandlers()
        {
            return (HandlerBlock[])handlers.ToArray(typeof(HandlerBlock));
        }

        internal void SetSize()
        {
            fatFormat = base.isFat();
            if (fatFormat) return;
            for (int i = 0; i < handlers.Count; i++)
            {
                HandlerBlock handler = (HandlerBlock)handlers[i];
                if (handler.isFat())
                {
                    fatFormat = true;
                    return;
                }
            }
        }

        internal int NumHandlers()
        {
            return handlers.Count;
        }

        internal override bool isFat()
        {
            return fatFormat;
        }

        internal void BuildTables(MetaDataOut md)
        {
            for (int i = 0; i < handlers.Count; i++)
            {
                ((HandlerBlock)handlers[i]).BuildTables(md);
            }
        }

        internal void BuildCILInfo(CILWriter output)
        {
            for (int i = 0; i < handlers.Count; i++)
            {
                ((HandlerBlock)handlers[i]).BuildCILInfo(output);
            }
        }

        internal override void Write(PEWriter output, bool fatFormat)
        {
            if (Diag.DiagOn) Console.WriteLine("writing exception details");
            for (int i = 0; i < handlers.Count; i++)
            {
                if (Diag.DiagOn) Console.WriteLine("Except block " + i);
                HandlerBlock handler = (HandlerBlock)handlers[i];
                flags = handler.GetFlag();
                if (Diag.DiagOn) Console.WriteLine("flags = " + flags);
                if (fatFormat) output.Write((uint)flags);
                else output.Write(flags);
                base.Write(output, fatFormat);
                handler.Write(output, fatFormat);
            }
        }
    }

    /**************************************************************************/
    public abstract class HandlerBlock : CodeBlock
    {
        protected static readonly ushort ExceptionFlag = 0;
        protected static readonly ushort FilterFlag = 0x01;
        protected static readonly ushort FinallyFlag = 0x02;
        protected static readonly ushort FaultFlag = 0x04;

        /*-------------------- Constructors ---------------------------------*/

        public HandlerBlock(CILLabel start, CILLabel end) : base(start, end) { }

        internal virtual ushort GetFlag()
        {
            if (Diag.DiagOn) Console.WriteLine("Catch Block");
            return ExceptionFlag;
        }

        internal virtual void BuildTables(MetaDataOut md) { }

        internal virtual void BuildCILInfo(CILWriter output) { }

        internal override void Write(PEWriter output, bool fatFormat)
        {
            base.Write(output, fatFormat);
        }

    }

    /**************************************************************************/
    /// <summary>
    /// The descriptor for a catch clause (.catch)
    /// </summary>
    public class Catch : HandlerBlock
    {
        Class exceptType;

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create a new catch clause
        /// </summary>
        /// <param name="except">the exception to be caught</param>
        /// <param name="handlerStart">start of the handler code</param>
        /// <param name="handlerEnd">end of the handler code</param>
        public Catch(Class except, CILLabel handlerStart, CILLabel handlerEnd)
            : base(handlerStart, handlerEnd)
        {
            exceptType = except;
        }

        internal override void BuildTables(MetaDataOut md)
        {
            if (!(exceptType is ClassDef)) exceptType.BuildMDTables(md);
        }

        internal override void BuildCILInfo(CILWriter output)
        {
            if (!(exceptType is ClassDef)) exceptType.BuildCILInfo(output);
        }

        internal override void Write(PEWriter output, bool fatFormat)
        {
            base.Write(output, fatFormat);
            output.Write(exceptType.Token());
        }
    }

    /**************************************************************************/
    /// <summary>
    /// The descriptor for a filter clause (.filter)
    /// </summary>
    public class Filter : HandlerBlock
    {
        CILLabel filterLabel;

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create a new filter clause
        /// </summary>
        /// <param name="filterLabel">the label where the filter code starts</param>
        /// <param name="handlerStart">the start of the handler code</param>
        /// <param name="handlerEnd">the end of the handler code</param>
        public Filter(CILLabel filterLabel, CILLabel handlerStart,
            CILLabel handlerEnd)
            : base(handlerStart, handlerEnd)
        {
            this.filterLabel = filterLabel;
        }

        internal override ushort GetFlag()
        {
            if (Diag.DiagOn) Console.WriteLine("Filter Block");
            return FilterFlag;
        }

        internal override void Write(PEWriter output, bool fatFormat)
        {
            base.Write(output, fatFormat);
            output.Write(filterLabel.GetLabelOffset());
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a finally block (.finally)
    /// </summary>
    public class Finally : HandlerBlock
    {

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create a new finally clause
        /// </summary>
        /// <param name="finallyStart">start of finally code</param>
        /// <param name="finallyEnd">end of finally code</param>
        public Finally(CILLabel finallyStart, CILLabel finallyEnd)
            : base(finallyStart, finallyEnd) { }

        internal override ushort GetFlag()
        {
            if (Diag.DiagOn) Console.WriteLine("Finally Block");
            return FinallyFlag;
        }

        internal override void Write(PEWriter output, bool fatFormat)
        {
            base.Write(output, fatFormat);
            output.Write((int)0);
        }

    }

    /**************************************************************************/
    /// <summary>
    /// Descriptor for a fault block (.fault)
    /// </summary>
    public class Fault : HandlerBlock
    {

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create a new fault clause
        /// </summary>
        /// <param name="faultStart">start of the fault code</param>
        /// <param name="faultEnd">end of the fault code</param>
        public Fault(CILLabel faultStart, CILLabel faultEnd)
            : base(faultStart, faultEnd) { }

        internal override ushort GetFlag()
        {
            if (Diag.DiagOn) Console.WriteLine("Fault Block");
            return FaultFlag;
        }

        internal override void Write(PEWriter output, bool fatFormat)
        {
            base.Write(output, fatFormat);
            output.Write((int)0);

        }
    }
}
