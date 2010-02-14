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
using System.Diagnostics;
using SCG = System.Collections.Generic;


namespace QUT.PERWAPI {


  /**************************************************************************/
  // Classes to represent CIL Byte Codes
  /**************************************************************************/
  /// <summary>
  /// The IL instructions for a method
  /// </summary>
  public class CILInstructions {
    private static readonly uint MaxClauses = 10;
    private static readonly uint ExHeaderSize = 4;
    private static readonly uint FatExClauseSize = 24;
    private static readonly uint SmlExClauseSize = 12;
    private static readonly sbyte maxByteVal = 127;
    private static readonly sbyte minByteVal = -128;
    private static readonly byte maxUByteVal = 255;
    private static readonly int smallSize = 64;
    internal static readonly ushort TinyFormat = 0x2;
    internal static readonly ushort FatFormat = 0x03;
    internal static readonly ushort FatFormatHeader = 0x3003;
    internal static readonly ushort MoreSects = 0x8;
    internal static readonly ushort InitLocals = 0x10;
    private static readonly uint FatSize = 12;
    private static readonly uint FatWords = FatSize / 4;
    internal static readonly byte FatExceptTable = 0x41;
    internal static readonly byte SmlExceptTable = 0x01;
    internal static readonly uint EHTable = 0x1;
    internal static readonly uint SectFatFormat = 0x40;
    internal static readonly uint SectMoreSects = 0x80;

    private ArrayList exceptions, sourceLines, defaultLines;
    private SourceFile defaultSourceFile;
    private Stack blockStack;
    //private bool codeChecked = false;
    private static readonly int INITSIZE = 5;
    private CILInstruction[] buffer = new CILInstruction[INITSIZE];
    // REPLACE with ArrayList<CILInstruction> for next version of .NET
    private CILInstruction[] saveBuffer;
    private int tide = 0, saveTide = 0;
    private uint offset = 0;
    private ushort headerFlags = 0;
    private short maxStack;
    private uint paddingNeeded = 0;
    private byte exceptHeader = 0;
    private int currI = -1;
    uint localSigIx = 0;
    int numReplace = 0;
    uint codeSize = 0, exceptSize = 0;
    bool tinyFormat, fatExceptionFormat = false, inserting = false;
    MethodDef thisMeth;

    internal Scope currentScope;

    /// <summary>
    /// Shows if return statements in this code block require a value on the stack or not.
    /// </summary>
    internal bool ReturnsVoid;

    /*-------------------- Constructors ---------------------------------*/

    internal CILInstructions(MethodDef meth) {
      thisMeth = meth;
    }

    /*--------------------- public general editing methods ---------------------------*/
    /// <summary>
    /// The source file containing these IL instructions
    /// </summary>
    public SourceFile DefaultSourceFile {
      get { return defaultSourceFile; }
      set { defaultSourceFile = value; }
    }

    /// <summary>
    /// The number of instructions currently in the buffer. 
    /// </summary>
    public int NumInstructions() {
      if (inserting) return tide + saveTide;
      return tide;
    }

    /// <summary>
    /// Get the next instruction in the instruction buffer in sequence.  
    /// An internal index is kept to keep track of which instruction was the last
    /// retrieved by this method.  On the first call, the first instruction in 
    /// the buffer is retrieved.  The instruction index may be zeroed 
    /// using ResetInstCounter().  This method cannot be called when in "insert" mode.
    /// </summary>
    /// <returns></returns>
    public CILInstruction GetNextInstruction() {
      if (inserting) throw new Exception("Cannot access next instruction during insert");
      if (currI + 1 < tide)
        return buffer[++currI];
      return null;
    }

    /// <summary>
    /// Get the previous instruction in the instruction buffer in sequence.  
    /// An internal index is kept to keep track of which instruction was the last
    /// retrieved by this method. This method cannot be called when in "insert" mode.
    /// </summary>
    /// <returns></returns>
    public CILInstruction GetPrevInstruction() {
      if (inserting) throw new Exception("Cannot access previous instruction during insert");
      if (currI > 0)
        return buffer[--currI];
      return null;
    }

    /// <summary>
    /// Reset the counter for GetNextInstuction to the first instruction.
    /// This method cannot be called when in "insert" mode.
    /// </summary>
    public void ResetInstCounter() {
      if (inserting) throw new Exception("Cannot reset instruction counter during insert");
      currI = -1;
    }

    /// <summary>
    /// Reset the counter for GetNextInstuction to the first instruction.
    /// This method cannot be called when in "insert" mode.
    /// </summary>
    public void EndInstCounter() {
      if (inserting) throw new Exception("Cannot reset instruction counter during insert");
      currI = tide;
    }

    /// <summary>
    /// Get all the IL instructions.
    /// This method cannot be called when in "insert" mode.
    /// </summary>
    /// <returns></returns>
    public CILInstruction[] GetInstructions() {
      if (inserting) throw new Exception("Cannot get instructions during insert");
      return buffer;
    }

    /// <summary>
    /// Set the instruction to be the new array of instructions, this will replace
    /// any existing instructions.  This method cannot be called when in "insert" mode.
    /// </summary>
    /// <param name="insts">The new instructions</param>
    public void SetInstructions(CILInstruction[] insts) {
      if (inserting) throw new Exception("Cannot replace instructions during insert.");
      buffer = insts;
      tide = buffer.Length;
      for (int i = 0; i < tide; i++) {
        if (insts[i] == null)
          tide = i;
        insts[i].index = (uint)i;
      }
    }

    /// <summary>
    /// This method should only be used to insert instructions into a buffer which 
    /// already contains some instructions.
    /// Start inserting instructions into the instruction buffer ie. set the buffer
    /// to "insert" mode.  The position of the insertion will be directly after 
    /// the "current instruction" as used be GetNextInstruction().  The 
    /// instructions to be inserted are any calls to the instruction specific 
    /// methods - Inst, TypeInst, MethodInst, etc.
    /// This method cannot be called if already in "insert" mode.
    /// </summary>
    public void StartInsert() {
      if (inserting)
        throw new Exception("Cannot insert into an instruction buffer already in insert mode");
      inserting = true;
      saveTide = tide;
      saveBuffer = buffer;
      tide = 0;
      buffer = new CILInstruction[INITSIZE];
    }

    /// <summary>
    /// Stop inserting instructions into the buffer.  Any instructions added after 
    /// this call will go at the end of the instruction buffer.  
    /// To be used with StartInsert().
    /// This method cannot be called if not in "insert" mode.
    /// </summary>
    public void EndInsert() {
      if (!inserting)
        throw new Exception("Cannot stop inserting if not in insert mode");
      CILInstruction[] newInsts = buffer;
      buffer = saveBuffer;
      int numNew = tide;
      tide = saveTide;
      int insPos = currI + 1;
      if (numReplace > 0) insPos--;
      InsertInstructions(insPos, newInsts, numNew);
      inserting = false;
      numReplace = 0;
    }

    /// <summary>
    /// Check if the buffer is ready for insertion of extra instructions.
    /// The buffer only needs to be in insert mode when instructions need
    /// to be added to existing instructions, not for addition of instructions
    /// to the end of the buffer.
    /// </summary>
    /// <returns></returns>
    public bool InInsertMode() { return inserting; }

    /// <summary>
    /// Remove the instruction at a specified position from the buffer.  If you 
    /// remove the "current" instruction (from GetNext or GetPrev) then the
    /// "current" instruction becomes the instruction before that in the buffer.
    /// </summary>
    /// <param name="pos">position of the instruction to be removed</param>
    public void RemoveInstruction(int pos) {
      if (pos < 0) return;
      for (int i = pos; i < tide - 1; i++) {
        buffer[i] = buffer[i + 1];
        buffer[i].index = (uint)i;
      }
      tide--;
      if (pos == currI) currI = pos - 1;
    }

    /// <summary>
    /// Remove the instructions from position "startRange" to (and including)
    /// position "endRange" from the buffer.  If the range removed contains the
    /// "current" instruction (from GetNext or GetPrev) then the "current" 
    /// instruction becomes the instruction before startRange in the buffer.
    /// </summary>
    public void RemoveInstructions(int startRange, int endRange) {
      if (startRange < 0) startRange = 0;
      if (endRange >= tide - 1) {// cut to startRange
        tide = startRange;
        return;
      }
      int offset = endRange - startRange + 1;
      for (int i = endRange + 1; i < tide; i++) {
        buffer[i - offset] = buffer[i];
        buffer[i - offset].index = (uint)(i - offset);
      }
      tide -= offset;
      if ((currI >= startRange) && (currI <= endRange)) currI = startRange - 1;
    }

    /// <summary>
    /// Replace a single IL instruction at position pos in the buffer 
    /// with some new instruction(s).  This removes the instruction and puts 
    /// the instruction buffer into "insert" mode at the position of the removed 
    /// instruction.  EndInsert must be called to insert the new instructions.
    /// This method cannot be called when in "insert" mode.
    /// </summary>
    /// <param name="pos">position of the instruction to be replaced</param>
    public void ReplaceInstruction(int pos) {
      if (inserting) throw new Exception("Cannot replace instructions during insert.");
      currI = pos;
      if ((pos > 0) || (pos < tide)) {
        numReplace = 1;
        StartInsert();
      }
    }

    /// <summary>
    /// Replace a number of IL instructions beginning at position pos in the buffer 
    /// with some new instruction(s).  This removes the instructions and puts 
    /// the instruction buffer into "insert" mode at the position of the removed 
    /// instructions.  EndInsert must be called to insert the new instructions.
    /// The instructions from index "from" up to and including index "to" will
    /// be replaced by the new instructions entered.
    /// This method cannot be called when in "insert" mode.
    /// </summary>
    /// <param name="from">the index to start replacing instruction from</param>
    /// <param name="to">the last index of the instructions to be replaced</param>
    public void ReplaceInstruction(int from, int to) {
      if (inserting) throw new Exception("Cannot replace instructions during insert.");
      currI = from;
      if ((from < 0) || (from >= tide) || (to < 0))
        throw new Exception("replace index is out of range");
      if (to >= tide) to = tide - 1;
      numReplace = to - from + 1;
      StartInsert();
    }

    /*---------------- public instruction specific methods ------------------------*/

    /// <summary>
    /// Add a simple IL instruction
    /// </summary>
    /// <param name="inst">the IL instruction</param>
    public void Inst(Op inst) {
      AddToBuffer(new Instr(inst));
    }

    /// <summary>
    /// Add an IL instruction with an integer parameter
    /// </summary>
    /// <param name="inst">the IL instruction</param>
    /// <param name="val">the integer parameter value</param>
    public void IntInst(IntOp inst, int val) {
      if ((inst == IntOp.ldc_i4_s) || (inst == IntOp.ldc_i4)) {
        if ((val < 9) && (val >= -1)) {
          AddToBuffer(new Instr((Op)((int)Op.ldc_i4_0 + val)));
        }
        else {
          AddToBuffer(new IntInstr(inst, val));
        }
      }
      else
        AddToBuffer(new UIntInstr(inst, (uint)val));
    }

    /// <summary>
    /// Add the load long instruction
    /// </summary>
    /// <param name="cVal">the long value</param>
    public void ldc_i8(long cVal) {
      AddToBuffer(new LongInstr(SpecialOp.ldc_i8, cVal));
    }

    /// <summary>
    /// Add the load float32 instruction
    /// </summary>
    /// <param name="cVal">the float value</param>
    public void ldc_r4(float cVal) {
      AddToBuffer(new FloatInstr(SpecialOp.ldc_r4, cVal));
    }

    /// <summary>
    /// Add the load float64 instruction
    /// </summary>
    /// <param name="cVal">the float value</param>
    public void ldc_r8(double cVal) {
      AddToBuffer(new DoubleInstr(SpecialOp.ldc_r8, cVal));
    }

    /// <summary>
    /// Add the load string instruction
    /// </summary>
    /// <param name="str">the string value</param>
    public void ldstr(string str) {
      AddToBuffer(new StringInstr(SpecialOp.ldstr, str));
    }

    /// <summary>
    /// Add the calli instruction
    /// </summary>
    /// <param name="sig">the signature for the calli</param>
    public void calli(CalliSig sig) {
      AddToBuffer(new SigInstr(SpecialOp.calli, sig));
    }

    /// <summary>
    /// Create a new CIL label.  To place the label in the CIL instruction
    /// stream use CodeLabel.
    /// </summary>
    /// <returns>a new CIL label</returns>
    public CILLabel NewLabel() {
      return new CILLabel();
    }

    /// <summary>
    /// Create a new label at this position in the code buffer
    /// </summary>
    /// <returns>the label at the current position</returns>
    public CILLabel NewCodedLabel() {
      CILLabel lab = new CILLabel();
      lab.Buffer = this;
      AddToBuffer(lab);
      return lab;
    }

    /// <summary>
    /// Add a label to the CIL instructions
    /// </summary>
    /// <param name="lab">the label to be added</param>
    public void CodeLabel(CILLabel lab) {
      if (lab.Buffer == null) {
        lab.Buffer = this;
      }
      else if (lab.Buffer != this) {
        throw new DescriptorException("Cannot add a label to two different code buffers");
      }
      AddToBuffer(lab);
    }

    /// <summary>
    /// Add an instruction with a field parameter
    /// </summary>
    /// <param name="inst">the CIL instruction</param>
    /// <param name="f">the field parameter</param>
    public void FieldInst(FieldOp inst, Field f) {
      Debug.Assert(f != null);
      if (f is FieldDef)
        if (((FieldDef)f).GetScope() != thisMeth.GetScope())
          throw new DescriptorException();
      AddToBuffer(new FieldInstr(inst, f));
    }

    /// <summary>
    /// Add an instruction with a method parameter
    /// </summary>
    /// <param name="inst">the CIL instruction</param>
    /// <param name="m">the method parameter</param>
    public void MethInst(MethodOp inst, Method m) {
      Debug.Assert(m != null);
      if (m is MethodDef)
        if (((MethodDef)m).GetScope() != thisMeth.GetScope())
          throw new DescriptorException();
      AddToBuffer(new MethInstr(inst, m));
    }

    /// <summary>
    /// Add an instruction with a type parameter
    /// </summary>
    /// <param name="inst">the CIL instruction</param>
    /// <param name="aType">the type argument for the CIL instruction</param>
    public void TypeInst(TypeOp inst, Type aType) {
      Debug.Assert(aType != null);
      if (aType is ClassDef) {
        if (((ClassDef)aType).GetScope() != thisMeth.GetScope())
          throw new DescriptorException();
      }
      AddToBuffer(new TypeInstr(inst, aType));
    }

    /// <summary>
    /// Add a branch instruction
    /// </summary>
    /// <param name="inst">the branch instruction</param>
    /// <param name="lab">the label that is the target of the branch</param>
    public void Branch(BranchOp inst, CILLabel lab) {
      Debug.Assert(lab != null);
      AddToBuffer(new BranchInstr(inst, lab));
    }

    /// <summary>
    /// Add a switch instruction
    /// </summary>
    /// <param name="labs">the target labels for the switch</param>
    public void Switch(CILLabel[] labs) {
      AddToBuffer(new SwitchInstr(labs));
    }

    /// <summary>
    /// Add a byte to the CIL instructions (.emitbyte)
    /// </summary>
    /// <param name="bVal"></param>
    public void emitbyte(byte bVal) {
      AddToBuffer(new CILByte(bVal));
    }

    /// <summary>
    /// Add an instruction which puts an integer on TOS.  This method
    /// selects the correct instruction based on the value of the integer.
    /// </summary>
    /// <param name="i">the integer value</param>
    public void PushInt(int i) {
      if (i == -1) {
        AddToBuffer(new Instr(Op.ldc_i4_m1));
      }
      else if ((i >= 0) && (i <= 8)) {
        Op op = (Op)(Op.ldc_i4_0 + i);
        AddToBuffer(new Instr(op));
      }
      else if ((i >= minByteVal) && (i <= maxByteVal)) {
        AddToBuffer(new IntInstr(IntOp.ldc_i4_s, i));
      }
      else {
        AddToBuffer(new IntInstr(IntOp.ldc_i4, i));
      }
    }

    /// <summary>
    /// Add the instruction to load a long on TOS
    /// </summary>
    /// <param name="l">the long value</param>
    public void PushLong(long l) {
      AddToBuffer(new LongInstr(SpecialOp.ldc_i8, l));
    }

    /// <summary>
    /// Add an instruction to push the boolean value true on TOS
    /// </summary>
    public void PushTrue() {
      AddToBuffer(new Instr(Op.ldc_i4_1));
    }

    /// <summary>
    ///  Add an instruction to push the boolean value false on TOS
    /// </summary>
    public void PushFalse() {
      AddToBuffer(new Instr(Op.ldc_i4_0));
    }

    /// <summary>
    /// Add the instruction to load an argument on TOS.  This method
    /// selects the correct instruction based on the value of argNo
    /// </summary>
    /// <param name="argNo">the number of the argument</param>
    public void LoadArg(int argNo) {
      if (argNo < 4) {
        Op op = (Op)Op.ldarg_0 + argNo;
        AddToBuffer(new Instr(op));
      }
      else if (argNo <= maxUByteVal) {
        AddToBuffer(new UIntInstr(IntOp.ldarg_s, (uint)argNo));
      }
      else {
        AddToBuffer(new UIntInstr(IntOp.ldarg, (uint)argNo));
      }
    }

    /// <summary>
    /// Add the instruction to load the address of an argument on TOS.
    /// This method selects the correct instruction based on the value
    /// of argNo.
    /// </summary>
    /// <param name="argNo">the number of the argument</param>
    public void LoadArgAdr(int argNo) {
      if (argNo <= maxUByteVal) {
        AddToBuffer(new UIntInstr(IntOp.ldarga_s, (uint)argNo));
      }
      else {
        AddToBuffer(new UIntInstr(IntOp.ldarga, (uint)argNo));
      }
    }

    /// <summary>
    /// Add the instruction to load a local on TOS.  This method selects
    /// the correct instruction based on the value of locNo.
    /// </summary>
    /// <param name="locNo">the number of the local to load</param>
    public void LoadLocal(int locNo) {
      if (locNo < 4) {
        Op op = (Op)Op.ldloc_0 + locNo;
        AddToBuffer(new Instr(op));
      }
      else if (locNo <= maxUByteVal) {
        AddToBuffer(new UIntInstr(IntOp.ldloc_s, (uint)locNo));
      }
      else {
        AddToBuffer(new UIntInstr(IntOp.ldloc, (uint)locNo));
      }
    }

    /// <summary>
    /// Add the instruction to load the address of a local on TOS.
    /// This method selects the correct instruction based on the 
    /// value of locNo.
    /// </summary>
    /// <param name="locNo">the number of the local</param>
    public void LoadLocalAdr(int locNo) {
      if (locNo <= maxUByteVal) {
        AddToBuffer(new UIntInstr(IntOp.ldloca_s, (uint)locNo));
      }
      else {
        AddToBuffer(new UIntInstr(IntOp.ldloca, (uint)locNo));
      }
    }

    /// <summary>
    /// Add the instruction to store to an argument.  This method
    /// selects the correct instruction based on the value of argNo.
    /// </summary>
    /// <param name="argNo">the argument to be stored to</param>
    public void StoreArg(int argNo) {
      if (argNo <= maxUByteVal) {
        AddToBuffer(new UIntInstr(IntOp.starg_s, (uint)argNo));
      }
      else {
        AddToBuffer(new UIntInstr(IntOp.starg, (uint)argNo));
      }
    }

    /// <summary>
    /// Add the instruction to store to a local.  This method selects
    /// the correct instruction based on the value of locNo.
    /// </summary>
    /// <param name="locNo">the local to be stored to</param>
    public void StoreLocal(int locNo) {
      if (locNo < 4) {
        Op op = (Op)Op.stloc_0 + locNo;
        AddToBuffer(new Instr(op));
      }
      else if (locNo <= maxUByteVal) {
        AddToBuffer(new UIntInstr(IntOp.stloc_s, (uint)locNo));
      }
      else {
        AddToBuffer(new UIntInstr(IntOp.stloc, (uint)locNo));
      }
    }

    public void IntLine(int num) {
      Line((uint)num, 1);
    }

    /// <summary>
    /// CLS compliant version of Line()
    /// </summary>
    /// <param name="sLin">The start line</param>
    /// <param name="sCol">The start column</param>
    /// <param name="eLin">The end line</param>
    /// <param name="eCol">The end column</param>
    public void IntLine(int sLin, int sCol, int eLin, int eCol) {
      Line((uint)sLin, (uint)sCol, (uint)eLin, (uint)eCol);
    }

    /// <summary>
    /// Create a new line instruction.
    /// </summary>
    /// <param name="num">The line for the given code segment.</param>
    /// <param name="startCol">The starting column for the code segment.</param>
    public void Line(uint num, uint startCol) {
      if (this.DefaultSourceFile == null) throw new Exception("Method can only be used if DefaultSourceFile has been set.");
      AddToBuffer(new Line(num, startCol, this.DefaultSourceFile));
    }

    /// <summary>
    /// Create a new line instruction.
    /// </summary>
    /// <param name="num">The line for the given code segment.</param>
    /// <param name="startCol">The starting column for the code segment.</param>
    /// <param name="endCol">The ending column for the code segment.</param>
    public void Line(uint num, uint startCol, uint endCol) {
      if (this.DefaultSourceFile == null) throw new Exception("Method can only be used if DefaultSourceFile has been set.");
      AddToBuffer(new Line(num, startCol, num, endCol, this.DefaultSourceFile));
    }

    /// <summary>
    /// Create a new line instruction.
    /// </summary>
    /// <param name="startNum">The starting line for the code segment.</param>
    /// <param name="startCol">The starting column for the code segment.</param>
    /// <param name="endNum">The ending line for the code segment.</param>
    /// <param name="endCol">The ending column for the code segment.</param>
    public void Line(uint startNum, uint startCol, uint endNum, uint endCol) {
      if (this.DefaultSourceFile == null) throw new Exception("Method can only be used if DefaultSourceFile has bene set.");
      AddToBuffer(new Line(startNum, startCol, endNum, endCol, this.DefaultSourceFile));
    }

    /// <summary>
    /// Create a new line instruction.
    /// </summary>
    /// <param name="startNum">The starting line for the code segment.</param>
    /// <param name="startCol">The starting column for the code segment.</param>
    /// <param name="endNum">The ending line for the code segment.</param>
    /// <param name="endCol">The ending column for the code segment.</param>
    /// <param name="sFile">The source file for the given code segment.</param>
    public void Line(uint startNum, uint startCol, uint endNum, uint endCol, SourceFile sFile) {
      AddToBuffer(new Line(startNum, startCol, endNum, endCol, sFile));
    }

    /// <summary>
    /// The current scope.
    /// </summary>
    public Scope CurrentScope {
      get { return currentScope; }
    }

    /// <summary>
    /// Open a new scope.
    /// </summary>
    public void OpenScope() {
      currentScope = new Scope(currentScope, thisMeth);
      AddToBuffer(new OpenScope(currentScope));
      //Console.WriteLine("Open scope on " + currentScope._thisMeth.Name());
    }

    /// <summary>
    /// Close the current scope.
    /// </summary>
    public void CloseScope() {
      //Console.WriteLine("Close scope on " + currentScope._thisMeth.Name());
      AddToBuffer(new CloseScope(currentScope));
      currentScope = currentScope._parent;
    }

    /// <summary>
    /// Bind a local to the CIL instructions.
    /// </summary>
    /// <param name="name">The name of the local variable..</param>
    /// <param name="index">The index of the local variable.</param>
    /// <returns>The LocalBinding object created with the given values.</returns>
    public LocalBinding BindLocal(string name, int index) {
      if (currentScope == null)
        throw new Exception("Scope must be opened before locals can be bound.");
      return currentScope.AddLocalBinding(name, index);
    }

    /// <summary>
    /// Bind a local to the CIL instructions.
    /// </summary>
    /// <param name="local">The local variable to load.</param>
    /// <returns>The LocalBinding object created for the given Local object.</returns>
    public LocalBinding BindLocal(Local local) {
      return BindLocal(local.Name, local.GetIndex());
    }

    /// <summary>
    /// Bind a constant to the CIL instructions.
    /// </summary>
    /// <param name="name">The name of the constant.</param>
    /// <param name="value">The value of the constant.</param>
    /// <param name="type">The type of the constant.</param>
    /// <returns>Return the ConstantBinding created with the given values.</returns>
    public ConstantBinding BindConstant(string name, object value, Type type) {
      if (currentScope == null)
        throw new Exception("Scope must be opened before constants can be bound.");
      return currentScope.AddConstantBinding(name, value, type);
    }

    /// <summary>
    /// Mark this position as the start of a new block
    /// (try, catch, filter, finally or fault)
    /// </summary>
    public void StartBlock() {
      if (blockStack == null) blockStack = new Stack();
      blockStack.Push(NewCodedLabel());
    }

    /// <summary>
    /// Mark this position as the end of the last started block and
    /// make it a try block.  This try block is added to the current 
    /// instructions (ie do not need to call AddTryBlock)
    /// </summary>
    /// <returns>The try block just ended</returns>
    public TryBlock EndTryBlock() {
      TryBlock tBlock = new TryBlock((CILLabel)blockStack.Pop(), NewCodedLabel());
      AddTryBlock(tBlock);
      return tBlock;
    }

    /// <summary>
    /// Mark this position as the end of the last started block and
    /// make it a catch block.  This catch block is associated with the
    /// specified try block.
    /// </summary>
    /// <param name="exceptType">the exception type to be caught</param>
    /// <param name="tryBlock">the try block associated with this catch block</param>
    public void EndCatchBlock(Class exceptType, TryBlock tryBlock) {
      Catch catchBlock = new Catch(exceptType, (CILLabel)blockStack.Pop(), NewCodedLabel());
      tryBlock.AddHandler(catchBlock);
    }

    /// <summary>
    /// Mark this position as the end of the last started block and
    /// make it a filter block.  This filter block is associated with the
    /// specified try block.  The format is:
    /// filterLab:   ...
    ///              ...
    /// filterHandler :  ...
    ///                  ...             
    /// </summary>
    /// <param name="filterLab">the label where the filter code is</param>
    /// <param name="tryBlock">the try block associated with this filter block</param>
    public void EndFilterBlock(CILLabel filterLab, TryBlock tryBlock) {
      Filter filBlock = new Filter(filterLab, (CILLabel)blockStack.Pop(), NewCodedLabel());
      tryBlock.AddHandler(filBlock);
    }

    /// <summary>
    /// Mark this position as the end of the last started block and
    /// make it a finally block.  This finally block is associated with the
    /// specified try block.
    /// </summary>
    /// <param name="tryBlock">the try block associated with this finally block</param>
    public void EndFinallyBlock(TryBlock tryBlock) {
      Finally finBlock = new Finally((CILLabel)blockStack.Pop(), NewCodedLabel());
      tryBlock.AddHandler(finBlock);
    }

    /// <summary>
    /// Mark this position as the end of the last started block and
    /// make it a fault block.  This fault block is associated with the
    /// specified try block.
    /// </summary>
    /// <param name="tryBlock">the try block associated with this fault block</param>
    public void EndFaultBlock(TryBlock tryBlock) {
      Fault fBlock = new Fault((CILLabel)blockStack.Pop(), NewCodedLabel());
      tryBlock.AddHandler(fBlock);
    }

    public void AddTryBlock(TryBlock tryBlock) {
      if (exceptions == null)
        exceptions = new ArrayList();
      else if (exceptions.Contains(tryBlock)) return;
      exceptions.Add(tryBlock);
    }

    /*------------------------- private methods ----------------------------*/

    private void AddToBuffer(CILInstruction inst) {
      if (tide >= buffer.Length) {
        CILInstruction[] tmp = buffer;
        buffer = new CILInstruction[tmp.Length * 2];
        for (int i = 0; i < tide; i++) {
          buffer[i] = tmp[i];
        }
      }
      //Console.WriteLine("Adding instruction at offset " + offset + " with size " + inst.size);
      //inst.offset = offset;
      //offset += inst.size;
      inst.index = (uint)tide;
      buffer[tide++] = inst;
    }

    private void UpdateIndexesFrom(int ix) {
      for (int i = ix; i < tide; i++) {
        buffer[i].index = (uint)i;
      }
    }

    private void InsertInstructions(int ix, CILInstruction[] newInsts, int numNew) {
      CILInstruction[] newBuff = buffer, oldBuff = buffer;
      int newSize = tide + numNew - numReplace;
      if (buffer.Length < newSize) {
        newBuff = new CILInstruction[newSize];
        for (int i = 0; i < ix; i++) {
          newBuff[i] = oldBuff[i];
        }
      }
      // shuffle up
      int offset = numNew - numReplace;
      int end = ix + numReplace;
      for (int i = tide - 1; i >= end; i--) {
        newBuff[i + offset] = oldBuff[i];
      }
      // insert new instructions
      for (int i = 0; i < numNew; i++) {
        newBuff[ix + i] = newInsts[i];
      }
      buffer = newBuff;
      tide += numNew - numReplace;
      UpdateIndexesFrom(ix);
    }

    internal bool IsEmpty() {
      return tide == 0;
    }

    internal static CILLabel GetLabel(ArrayList labs, uint targetOffset) {
      CILLabel lab;
      int i = 0;
      while ((i < labs.Count) && (((CILLabel)labs[i]).offset < targetOffset)) i++;
      if (i < labs.Count) {
        if (((CILLabel)labs[i]).offset == targetOffset) // existing label
          lab = (CILLabel)labs[i];
        else {
          lab = new CILLabel(targetOffset);
          labs.Insert(i, lab);
        }
      }
      else {
        lab = new CILLabel(targetOffset);
        labs.Add(lab);
      }
      return lab;
    }

    internal void AddEHClause(EHClause ehc) {
      if (exceptions == null)
        exceptions = new ArrayList();
      exceptions.Add(ehc);
    }

    internal void SetAndResolveInstructions(CILInstruction[] insts) {
      offset = 0;
      ArrayList labels = new ArrayList();
      for (int i = 0; i < insts.Length; i++) {
        insts[i].offset = offset;
        offset += insts[i].size;
        if (insts[i] is BranchInstr) {
          ((BranchInstr)insts[i]).MakeTargetLabel(labels);
        }
        else if (insts[i] is SwitchInstr) {
          ((SwitchInstr)insts[i]).MakeTargetLabels(labels);
        }
      }
      if (exceptions != null) {
        for (int i = 0; i < exceptions.Count; i++) {
          exceptions[i] = ((EHClause)exceptions[i]).MakeTryBlock(labels);
        }
      }
      if (labels.Count == 0) { buffer = insts; tide = buffer.Length; return; }
      buffer = new CILInstruction[insts.Length + labels.Count];
      int currentPos = 0;
      tide = 0;
      for (int i = 0; i < labels.Count; i++) {
        CILLabel lab = (CILLabel)labels[i];
        while ((currentPos < insts.Length) && (insts[currentPos].offset < lab.offset))
          buffer[tide++] = insts[currentPos++];
        buffer[tide++] = lab;
      }
      while (currentPos < insts.Length) {
        buffer[tide++] = insts[currentPos++];
      }
    }

    internal uint GetCodeSize() {
      return codeSize + paddingNeeded + exceptSize;
    }

    internal void BuildTables(MetaDataOut md) {
      for (int i = 0; i < tide; i++) {
        buffer[i].BuildTables(md);
      }
      if (exceptions != null) {
        for (int i = 0; i < exceptions.Count; i++) {
          ((TryBlock)exceptions[i]).BuildTables(md);
        }
      }
    }

    internal void BuildCILInfo(CILWriter output) {
      for (int i = 0; i < tide; i++) {
        buffer[i].BuildCILInfo(output);
      }
      if (exceptions != null) {
        for (int i = 0; i < exceptions.Count; i++) {
          ((TryBlock)exceptions[i]).BuildCILInfo(output);
        }
      }
    }

    internal void ChangeRefsToDefs(ClassDef newType, ClassDef[] oldTypes) {
      for (int i = 0; i < tide; i++) {
        if (buffer[i] is SigInstr) {
          CalliSig sig = ((SigInstr)buffer[i]).GetSig();
          sig.ChangeRefsToDefs(newType, oldTypes);
        }
        else if (buffer[i] is TypeInstr) {
          TypeInstr tinst = (TypeInstr)buffer[i];
          if (tinst.GetTypeArg() is ClassDef) {
            ClassDef iType = (ClassDef)tinst.GetTypeArg();
            bool changed = false;
            for (int j = 0; (j < oldTypes.Length) && !changed; j++) {
              if (iType == oldTypes[j])
                tinst.SetTypeArg(newType);
            }
          }
        }
      }
    }

    internal void AddToLines(Line line) {
      if ((line.sourceFile == null) || (line.sourceFile.Match(defaultSourceFile))) {
        if (defaultLines == null) {
          if (defaultSourceFile == null)
            throw new Exception("No Default Source File Set");
          defaultLines = new ArrayList();
        }
        defaultLines.Add(line);
        return;
      }
      if (sourceLines == null) {
        sourceLines = new ArrayList();
      }
      else {
        for (int i = 0; i < sourceLines.Count; i++) {
          ArrayList lineList = (ArrayList)sourceLines[i];
          if (((Line)lineList[0]).sourceFile.Match(line.sourceFile)) {
            lineList.Add(line);
            return;
          }
        }
        ArrayList newList = new ArrayList();
        newList.Add(line);
        sourceLines.Add(newList);
      }
    }

    internal void CheckCode(uint locSigIx, bool initLocals, int maxStack, MetaDataOut metaData) {
      if (tide == 0) return;
      offset = 0;
      for (int i = 0; i < tide; i++) {
        buffer[i].offset = offset;
        offset += buffer[i].size;
        if (buffer[i] is Line)
          AddToLines((Line)buffer[i]);
      }
      bool changed = true;
      while (changed) {
        changed = false;
        Line prevLine = null;
        for (int i = 0; i < tide; i++) {
          if (buffer[i] is Line) {
            if (prevLine != null)
              prevLine.CalcEnd((Line)buffer[i]);
            prevLine = (Line)buffer[i];
          }
          changed = buffer[i].Check(metaData) || changed;
        }
        if (prevLine != null) prevLine.Last();
        if (changed) {
          for (int i = 1; i < tide; i++) {
            buffer[i].offset = buffer[i - 1].offset + buffer[i - 1].size;
          }
          offset = buffer[tide - 1].offset + buffer[tide - 1].size;
        }
      }
      codeSize = offset;
      if (Diag.DiagOn) Console.WriteLine("codeSize before header added = " + codeSize);
      if (maxStack == 0) this.maxStack = 8;
      else this.maxStack = (short)maxStack;
      if ((offset < smallSize) && (maxStack <= 8) && (locSigIx == 0) && (exceptions == null)) {
        // can use tiny header
        if (Diag.DiagOn) Console.WriteLine("Tiny Header");
        tinyFormat = true;
        headerFlags = (ushort)(TinyFormat | ((ushort)codeSize << 2));
        codeSize++;
        if ((codeSize % 4) != 0) { paddingNeeded = 4 - (codeSize % 4); }
      }
      else {
        if (Diag.DiagOn) Console.WriteLine("Fat Header");
        tinyFormat = false;
        localSigIx = locSigIx;
        //this.maxStack = (short)maxStack;
        headerFlags = FatFormatHeader;
        if (exceptions != null) {
          // Console.WriteLine("Got exceptions");
          headerFlags |= MoreSects;
          uint numExceptClauses = 0;
          for (int i = 0; i < exceptions.Count; i++) {
            TryBlock tryBlock = (TryBlock)exceptions[i];
            tryBlock.SetSize();
            numExceptClauses += (uint)tryBlock.NumHandlers();
            if (tryBlock.isFat()) fatExceptionFormat = true;
          }
          if (numExceptClauses > MaxClauses) fatExceptionFormat = true;
          if (Diag.DiagOn) Console.WriteLine("numexceptclauses = " + numExceptClauses);
          if (fatExceptionFormat) {
            if (Diag.DiagOn) Console.WriteLine("Fat exception format");
            exceptHeader = FatExceptTable;
            exceptSize = ExHeaderSize + numExceptClauses * FatExClauseSize;
          }
          else {
            if (Diag.DiagOn) Console.WriteLine("Tiny exception format");
            exceptHeader = SmlExceptTable;
            exceptSize = ExHeaderSize + numExceptClauses * SmlExClauseSize;
          }
          if (Diag.DiagOn) Console.WriteLine("exceptSize = " + exceptSize);
        }
        if (initLocals) headerFlags |= InitLocals;
        if ((offset % 4) != 0) { paddingNeeded = 4 - (offset % 4); }
        codeSize += FatSize;
      }
      if (Diag.DiagOn)
        Console.WriteLine("codeSize = " + codeSize + "  headerFlags = " + Hex.Short(headerFlags));
    }

    /// <summary>
    /// Returns the maximum stack depth required by these CIL instructions.
    /// </summary>
    /// <returns>The integer value of the stck depth.</returns>
    public int GetMaxStackDepthRequired() {
      if (tide == 0) return 0;

      // Store the code blocks we find
      SCG.List<CodeBlock> codeBlocks = new SCG.List<CodeBlock>();
      SCG.Dictionary<CILLabel, CodeBlock> cbTable = new SCG.Dictionary<CILLabel, CodeBlock>();
      SCG.List<CodeBlock> extraStartingBlocks = new SCG.List<CodeBlock>();

      // Start a default code block
      CodeBlock codeBlock = new CodeBlock(this);
      codeBlock.StartIndex = 0;

      //
      // Identify the code blocks
      //
      for (int i = 0; i < tide; i++) {

        /* Handling the tail instruction:
         * The tail instruction has not been handled even though
         * it indicates the end of a code block is coming.  The
         * reason for this is because any valid tail instruction
         * must be followed by a call* instruction and then a ret
         * instruction.  Given a ret instruction must be the second
         * next instruction anyway it has been decided to just let 
         * the end block be caught then.
         */

        // If we reach a branch instruction or a switch instruction 
        // then end the current code block inclusive of the instruction.
        if ((buffer[i] is BranchInstr) || (buffer[i] is SwitchInstr)) {

          // Close the old block
          codeBlock.EndIndex = i;
          if (codeBlock.EndIndex >= codeBlock.StartIndex) // Don't add empty blocks
            codeBlocks.Add(codeBlock);

          // Open a new block
          codeBlock = new CodeBlock(this);
          codeBlock.StartIndex = i + 1;

          // If we reach a label then we need to start a new
          // code block as the label is an entry point.
        }
        else if (buffer[i] is CILLabel) {

          // Close the old block
          codeBlock.EndIndex = i - 1;
          if (codeBlock.EndIndex >= codeBlock.StartIndex) // Don't add empty blocks
            codeBlocks.Add(codeBlock);

          // Open a new block
          codeBlock = new CodeBlock(this);
          codeBlock.StartIndex = i;

          // Set this label as the entry point for the code block
          codeBlock.EntryLabel = (CILLabel)buffer[i];
          // AND ... list in the dictionary.
          cbTable.Add(codeBlock.EntryLabel, codeBlock);

          // Check for the ret, throw, rethrow, or jmp instruction as they also end a block
        }
        else if (buffer[i] is Instr) {
          if (
              (((Instr)buffer[i]).GetOp() == Op.ret) ||
              (((Instr)buffer[i]).GetOp() == Op.throwOp) ||
              (((Instr)buffer[i]).GetOp() == Op.rethrow) ||
              ((buffer[i] is MethInstr) && (((MethInstr)buffer[i]).GetMethodOp() == MethodOp.jmp))
             ) {

            // Close the old block
            codeBlock.EndIndex = i;
            if (codeBlock.EndIndex >= codeBlock.StartIndex) // Don't add empty blocks
              codeBlocks.Add(codeBlock);

            // Open a new block
            // In theory this should never happen but just in case
            // someone feels like adding dead code it is supported.
            codeBlock = new CodeBlock(this);
            codeBlock.StartIndex = i + 1;

          }

        }

      }

      // Close the last block
      codeBlock.EndIndex = tide - 1;
      if (codeBlock.EndIndex >= codeBlock.StartIndex) // Don't add empty blocks
        codeBlocks.Add(codeBlock);
      codeBlock = null;

      // Check how many code blocks there are.  If an blocks return 0.
      if (codeBlocks.Count == 0) return 0;

      //
      // Loop through each code block and calculate the delta distance
      //
      for (int j = 0; j < codeBlocks.Count; j++) {
        CodeBlock block = codeBlocks[j];

        int maxDepth = 0;
        int currentDepth = 0;

        // Loop through each instruction to work out the max depth
        for (int i = block.StartIndex; i <= block.EndIndex; i++) {

          // Get the depth after the next instruction
          currentDepth += buffer[i].GetDeltaDistance();

          // If the new current depth is greater then the maxDepth adjust the maxDepth to reflect
          if (currentDepth > maxDepth)
            maxDepth = currentDepth;

        }

        // Set the depth of the block
        block.MaxDepth = maxDepth;
        block.DeltaDistance = currentDepth;

        //
        // Link up the next blocks
        //

        // If the block ends with a branch statement set the jump and fall through.
        if (buffer[block.EndIndex] is BranchInstr) {
          BranchInstr branchInst = (BranchInstr)buffer[block.EndIndex];

          // If this is not a "br" or "br.s" then set the fall through code block
          if ((branchInst.GetBranchOp() != BranchOp.br) &&
              (branchInst.GetBranchOp() != BranchOp.br_s))
            // If there is a following code block set it as the fall through
            if (j < (codeBlocks.Count - 1))
              block.NextBlocks.Add(codeBlocks[j + 1]);

          // Set the code block we are jumping to
          CodeBlock cb = null;
          cbTable.TryGetValue(branchInst.GetDest(), out cb);
          if (cb == null)
            throw new Exception("Missing Branch Label");
          block.NextBlocks.Add(cb);

          // If the block ends in a switch instruction work out the possible next blocks
        }
        else if (buffer[block.EndIndex] is SwitchInstr) {
          SwitchInstr switchInstr = (SwitchInstr)buffer[block.EndIndex];

          // If there is a following code block set it as the fall through
          if (j < (codeBlocks.Count - 1))
            block.NextBlocks.Add(codeBlocks[j + 1]);

          // Add each destination block
          foreach (CILLabel label in switchInstr.GetDests()) {

            // Check all of the code blocks to find the jump destination
            CodeBlock cb = null;
            cbTable.TryGetValue(label, out cb);
            if (cb == null) throw new Exception("Missing Case Label");
            block.NextBlocks.Add(cb);

          }

          // So long as the block doesn't end with a terminating instruction like ret or throw, just fall through to the next block
        }
        else if (!IsTerminatingInstruction(buffer[block.EndIndex])) {

          // If there is a following code block set it as the fall through
          if (j < (codeBlocks.Count - 1))
            block.NextBlocks.Add(codeBlocks[j + 1]);
        }

      }

      //
      // Join up any exception blocks
      //

      if (exceptions != null) {
        foreach (TryBlock tryBlock in exceptions) {

          // Try to find the code block where this try block starts
          CodeBlock tryCodeBlock;
          cbTable.TryGetValue(tryBlock.Start, out tryCodeBlock);

          // Declare that the entry to this code block must be empty
          tryCodeBlock.RequireEmptyEntry = true;

          // Work with each of the handlers
          foreach (HandlerBlock hb in tryBlock.GetHandlers()) {

            // Find the code block where this handler block starts.
            CodeBlock handlerCodeBlock;
            cbTable.TryGetValue(hb.Start, out handlerCodeBlock);

            // If the code block is a catch or filter block increment the delta 
            // distance by 1. This is to factor in the exception object that will 
            // be secretly placed on the stack by the runtime engine.
            // However, this also means that the MaxDepth is up by one also!
            if (hb is Catch || hb is Filter) {
              handlerCodeBlock.DeltaDistance++;
              handlerCodeBlock.MaxDepth++;
            }

            // If the code block is a filter block increment the delta distance by 1
            // This is to factor in the exception object that will be placed on the stack.
            // if (hb is Filter) handlerCodeBlock.DeltaDistance++;

            // Add this handler to the list of starting places
            extraStartingBlocks.Add(handlerCodeBlock);

          }

        }
      }


      //
      // Traverse the code blocks and get the depth
      //

      // Get the max depth at the starting entry point
      int finalMaxDepth = this.TraverseMaxDepth(codeBlocks[0]);

      // Check the additional entry points
      // If the additional points have a greater depth update the max depth
      foreach (CodeBlock cb in extraStartingBlocks) {
        // int tmpMaxDepth = cb.TraverseMaxDepth();
        int tmpMaxDepth = this.TraverseMaxDepth(cb);
        if (tmpMaxDepth > finalMaxDepth) finalMaxDepth = tmpMaxDepth;
      }

      // Return the max depth we have found
      return finalMaxDepth;

    }


    int TraverseMaxDepth(CodeBlock entryBlock) {
      int max = 0;
      SCG.Queue<CodeBlock> worklist = new SCG.Queue<CodeBlock>();
      entryBlock.Visited = true;
      entryBlock.LastVisitEntryDepth = 0;
      worklist.Enqueue(entryBlock);
      while (worklist.Count > 0) {
        int count = worklist.Count;
        CodeBlock unit = worklist.Dequeue();

        int maxDepth = unit.LastVisitEntryDepth + unit.MaxDepth;
        int exitDepth = unit.LastVisitEntryDepth + unit.DeltaDistance;

        if (maxDepth > max) max = maxDepth;

        foreach (CodeBlock succ in unit.NextBlocks) {
          if (succ.Visited) {
            if (succ.LastVisitEntryDepth != exitDepth)
              throw new InvalidStackDepth("inconsistent stack depth at offset " + succ.StartIndex.ToString());
          }
          else {
            succ.Visited = true;
            succ.LastVisitEntryDepth = exitDepth;
            worklist.Enqueue(succ);
          }
        }
      }
      return max;
    }

    private bool IsTerminatingInstruction(CILInstruction cilInstr) {
      // Return or throw instructions are terminating instructions
      if (cilInstr is Instr) {
        if (((Instr)cilInstr).GetOp() == Op.ret) return true;
        if (((Instr)cilInstr).GetOp() == Op.throwOp) return true;
        if (((Instr)cilInstr).GetOp() == Op.rethrow) return true;
      }
      // jmp is a terminating instruction
      if (cilInstr is MethInstr) {
        if (((MethInstr)cilInstr).GetMethodOp() == MethodOp.jmp) return true;
      }
      return false;
    }

    internal void Write(PEWriter output) {
      if (Diag.DiagOn) Console.WriteLine("Writing header flags = " + Hex.Short(headerFlags));
      if (tinyFormat) {
        if (Diag.DiagOn) Console.WriteLine("Writing tiny code");
        output.Write((byte)headerFlags);
      }
      else {
        if (Diag.DiagOn) Console.WriteLine("Writing fat code");
        output.Write(headerFlags);
        output.Write((ushort)maxStack);
        output.Write(offset);
        output.Write(localSigIx);
      }
      if (Diag.DiagOn) {
        Console.WriteLine(Hex.Int(tide) + " CIL instructions");
        Console.WriteLine("starting instructions at " + output.Seek(0, SeekOrigin.Current));
      }

      // Added to enable PDB generation
      if (output.pdbWriter != null) {

        // Open the method
        output.pdbWriter.OpenMethod((int)thisMeth.Token());

        // Check if this is the entry point method
        if (thisMeth.HasEntryPoint()) output.pdbWriter.SetEntryPoint((int)thisMeth.Token());
      }

      // Write out each memember of the buffer
      for (int i = 0; i < tide; i++) {
        buffer[i].Write(output);
      }

      // Added to enable PDB generation
      if (output.pdbWriter != null && tide > 0) {
        output.pdbWriter.CloseMethod();
      }
      if (Diag.DiagOn) Console.WriteLine("ending instructions at " + output.Seek(0, SeekOrigin.Current));
      for (int i = 0; i < paddingNeeded; i++) { output.Write((byte)0); }
      if (exceptions != null) {
        // Console.WriteLine("Writing exceptions");
        // Console.WriteLine("header = " + Hex.Short(exceptHeader) + " exceptSize = " + Hex.Int(exceptSize));
        output.Write(exceptHeader);
        output.Write3Bytes((uint)exceptSize);
        for (int i = 0; i < exceptions.Count; i++) {
          TryBlock tryBlock = (TryBlock)exceptions[i];
          tryBlock.Write(output, fatExceptionFormat);
        }
      }
    }

    internal void Write(CILWriter output) {
      for (int i = 0; i < tide; i++) {
        if (!(buffer[i] is CILLabel)) {
          output.Write("    ");
        }
        output.Write("    ");
        buffer[i].Write(output);
      }
      if (exceptions != null) {
        throw new NotYetImplementedException("Exceptions not yet implemented for CIL Instructions");
        // Console.WriteLine("Writing exceptions");
        // Console.WriteLine("header = " + Hex.Short(exceptHeader) + " exceptSize = " + Hex.Int(exceptSize));
        //output.Write(exceptHeader);
        //output.Write3Bytes((uint)exceptSize);
        //for (int i = 0; i < exceptions.Count; i++) {
        //     TryBlock tryBlock = (TryBlock)exceptions[i];
        //    tryBlock.Write(output, fatExceptionFormat);
        //}
      }
    }

    /// <summary>
    /// Stores the details of a given code block
    /// </summary>
    private class CodeBlock {
      internal int StartIndex;
      internal int EndIndex;
      internal int DeltaDistance;
      internal int MaxDepth;
      internal CILLabel EntryLabel;
      internal ArrayList NextBlocks = new ArrayList(); // List of CodeBlock objects
      // internal int Visits;
      internal int LastVisitEntryDepth;
      internal bool RequireEmptyEntry;
      internal bool Visited = false;
      private CILInstructions cilInstr;

      /// <summary>
      /// Create a new code block definition
      /// </summary>
      /// <param name="instructions">The buffer the code block relates to</param>
      internal CodeBlock(CILInstructions instructions) {
        cilInstr = instructions;
      }
    }
  }
  /**************************************************************************/
  /// <summary>
  /// Descriptor for an IL instruction
  /// </summary>
  public abstract class CILInstruction {
    protected static readonly sbyte maxByteVal = 127;
    protected static readonly sbyte minByteVal = -128;
    protected static readonly byte leadByte = 0xFE;
    protected static readonly uint USHeapIndex = 0x70000000;
    protected static readonly uint longInstrStart = (uint)Op.arglist;
    protected static readonly string[] opcode = { 
            "nop",     "break",   "ldarg.0", "ldarg.1", "ldarg.2", "ldarg.3", "ldloc.0", "ldloc.1",
            "ldloc.2", "ldloc.3", "stloc.0", "stloc.1", "stloc.2", "stloc.3", "ldarg.s", "ldarga.s",
            "starg.s", "ldloc.s", "ldloca.s","stloc.s", "ldnull",  "ldc.i4.m1","ldc.i4.0","ldc.i4.1",
            "ldc.i4.2","ldc.i4.3","ldc.i4.4","ldc.i4.5","ldc.i4.6","ldc.i4.7","ldc.i4.8","ldc.i4.s",
            "ldc.i4",  "ldc.i8",  "ldc.r4",  "ldc.r8",  "ERROR",   "dup",     "pop",     "jmp",     
            "call",    "calli",   "ret",     "br.s",   "brfalse.s","brtrue.s","beq.s",   "bge.s",
            "bgt.s",   "ble.s",   "blt.s",   "bne.un.s","bge.un.s","bgt.un.s","ble.un.s","blt.un.s",
            "br",      "brfalse", "brtrue",  "beq",     "bge",     "bgt",     "ble",     "blt",
            "bne.un",  "bge.un",  "bgt.un",  "ble.un",  "blt.un",  "switch",  "ldind.i1","ldind.u1",
            "ldind.i2","ldind.u2","ldind.i4","ldind.u4","ldind.i8","ldind.i", "ldind.r4","ldind.r8",
            "ldind.ref","stind.ref","stind.i1","stind.i2","stind.i4","stind.i8","stind.r4","stind.r8",
            "add",     "sub",     "mul",     "div",     "div.un",  "rem",     "rem.un",  "and",
            "or",      "xor",     "shl",     "shr",     "shr.un",  "neg",     "not",     "conv.i1",
            "conv.i2", "conv.i4", "conv.i8", "conv.r4", "conv.r8", "conv.u4", "conv.u8", "callvirt",
            "cpobj",   "ldobj",   "ldstr",   "newobj", "castclass","isinst",  "conv.r.un","ERROR",
            "ERROR",   "unbox",   "throw",   "ldfld",   "ldflda",  "stfld",   "ldsfld",  "ldsflda",
            "stsfld",             "stobj",              "conv.ovf.i1.un",         "conv.ovf.i2.un",
            "conv.ovf.i4.un",   "conv.ovf.i8.un",     "conv.ovf.u1.un",         "conv.ovf.u2.un",
            "conv.ovf.u4.un",     "conv.ovf.u8.un",     "conv.ovf.i.un",          "conv.ovf.u.un",
            "box",              "newarr",             "ldlen",                  "ldelema",
            "ldelem.i1",          "ldelem.u1",          "ldelem.i2",              "ldelem.u2",
            "ldelem.i4",        "ldelem.u4",          "ldelem.i8",              "ldelem.i",
            "ldelem.r4",          "ldelem.r8",          "ldelem.ref",             "stelem.i",
            "stelem.i1",        "stelem.i2",          "stelem.i4",              "stelem.i8",
            "stelem.r4",          "stelem.r8",          "stelem.ref",             "ERROR",
            "ERROR",            "ERROR",              "ERROR",                  "ERROR",
            "ERROR",   "ERROR",   "ERROR",   "ERROR",   "ERROR",   "ERROR",   "ERROR",   "ERROR",
            "ERROR",              "ERROR",              "ERROR",                  "conv.ovf.i1",
            "conv.ovf.u1",      "conv.ovf.i2",        "conv.ovf.u2",            "conv.ovf.i4",
            "conv.ovf.u4",        "conv.ovf.i8",        "conv.ovf.u8",            "ERROR",
            "ERROR",            "ERROR",              "ERROR",                  "ERROR",
            "ERROR",   "ERROR",  "refanyval","ckfinite","ERROR",   "ERROR",   "mkrefany","ERROR",
            "ERROR",   "ERROR",   "ERROR",   "ERROR",   "ERROR",   "ERROR",   "ERROR",   "ERROR",
            "ldtoken","conv.u2","conv.u1","conv.i","conv.ovf.i","conv.ovf.u","add.ovf","add.ovf.un",
            "mul.ovf","mul.ovf.un","sub.ovf","sub.ovf.un","endfinally","leave","leave.s","stind.i",
            "conv.u"};

    protected static readonly int[] opDeltaDistance = { 
              0 /* nop */,             0 /* break */,           1 /* ldarg.0 */,         1 /* ldarg.1 */,         1 /* ldarg.2 */,   1 /* ldarg.3 */,    1 /* ldloc.0 */,     1 /* ldloc.1 */,
              1 /* ldloc.2 */,         1 /* ldloc.3 */,        -1 /* stloc.0 */,        -1 /* stloc.1 */,        -1 /* stloc.2 */,  -1 /* stloc.3 */,    1 /* ldarg.s */,     1 /* ldarga.s */,
             -1 /* starg.s */,         1 /* ldloc.s */,         1 /* ldloca.s */,       -1 /* stloc.s */,         1 /* ldnull */,    1 /* ldc.i4.m1 */,  1 /* ldc.i4.0 */,    1 /* ldc.i4.1 */,
              1 /* ldc.i4.2 */,        1 /* ldc.i4.3 */,        1 /* ldc.i4.4 */,        1 /* ldc.i4.5 */,        1 /* ldc.i4.6 */,  1 /* ldc.i4.7 */,   1 /* ldc.i4.8 */,    1 /* ldc.i4.s */,
              1 /* ldc.i4 */,          1 /* ldc.i8 */,          1 /* ldc.r4 */,          1 /* ldc.r8 */,        -99 /* ERROR */,     1 /* dup */,       -1 /* pop */,         0 /* jmp */,     
            -99 /* call */,          -99 /* calli */,           0 /* ret */,             0 /* br.s */,           -1 /* brfalse.s */,-1 /* brtrue.s */,  -2 /* beq.s */,      -2 /* bge.s */,
             -2 /* bgt.s */,          -2 /* ble.s */,          -2 /* blt.s */,          -2 /* bne.un.s */,       -2 /* bge.un.s */, -2 /* bgt.un.s */,  -2 /* ble.un.s */,   -2 /* blt.un.s */,
              0 /* br */,             -1 /* brfalse */,        -1 /* brtrue */,         -2 /* beq */,            -2 /* bge */,      -2 /* bgt */,       -2 /* ble */,        -2 /* blt */,
             -2 /* bne.un */,         -2 /* bge.un */,         -2 /* bgt.un */,         -2 /* ble.un */,         -2 /* blt.un */,   -1 /* switch */,     0 /* ldind.i1 */,    0 /* ldind.u1 */,
              0 /* ldind.i2 */,        0 /* ldind.u2 */,        0 /* ldind.i4 */,        0 /* ldind.u4 */,        0 /* ldind.i8 */,  0 /* ldind.i */,    0 /* ldind.r4 */,    0 /* ldind.r8 */,
              0 /* ldind.ref */,      -2 /* stind.ref */,      -2 /* stind.i1 */,       -2 /* stind.i2 */,       -2 /* stind.i4 */, -2 /* stind.i8 */,  -2 /* stind.r4 */,   -2 /* stind.r8 */,
             -1 /* add */,            -1 /* sub */,            -1 /* mul */,            -1 /* div */,            -1 /* div.un */,   -1 /* rem */,       -1 /* rem.un */,     -1 /* and */,
             -1 /* or */,             -1 /* xor */,            -1 /* shl */,            -1 /* shr */,            -1 /* shr.un */,    0 /* neg */,        0 /* not */,         0 /* conv.i1 */,
              0 /* conv.i2 */,         0 /* conv.i4 */,         0 /* conv.i8 */,         0 /* conv.r4 */,         0 /* conv.r8 */,   0 /* conv.u4 */,    0 /* conv.u8 */,   -99 /* callvirt */,
             -2 /* cpobj */,           0 /* ldobj */,           1 /* ldstr */,         -99 /* newobj */,          0 /* castclass */, 0 /* isinst */,     0 /* conv.r.un */, -99 /* ERROR */,
            -99 /* ERROR */,           0 /* unbox */,          -1 /* throw */,           0 /* ldfld */,           0 /* ldflda */,   -2 /* stfld */,      1 /* ldsfld */,      1 /* ldsflda */,
             -1 /* stsfld */,         -2 /* stobj */,           0 /* conv.ovf.i1.un */,  0 /* conv.ovf.i2.un */,
              0 /* conv.ovf.i4.un */,  0 /* conv.ovf.i8.un */,  0 /* conv.ovf.u1.un */,  0 /* conv.ovf.u2.un */,
              0 /* conv.ovf.u4.un */,  0 /* conv.ovf.u8.un */,  0 /* conv.ovf.i.un */,   0 /* conv.ovf.u.un */,
              0 /* box */,             0 /* newarr */,          0 /* ldlen */,          -1 /* ldelema */,
             -1 /* ldelem.i1 */,      -1 /* ldelem.u1 */,      -1 /* ldelem.i2 */,      -1 /* ldelem.u2 */,
             -1 /* ldelem.i4 */,      -1 /* ldelem.u4 */,      -1 /* ldelem.i8 */,      -1 /* ldelem.i */,
             -1 /* ldelem.r4 */,      -1 /* ldelem.r8 */,      -1 /* ldelem.ref */,     -3 /* stelem.i */,
             -3 /* stelem.i1 */,      -3 /* stelem.i2 */,      -3 /* stelem.i4 */,      -3 /* stelem.i8 */,
             -3 /* stelem.r4 */,      -3 /* stelem.r8 */,      -3 /* stelem.ref */,    -99 /* ERROR */,
            -99 /* ERROR */,         -99 /* ERROR */,         -99 /* ERROR */,         -99 /* ERROR */,
            -99 /* ERROR */,         -99 /* ERROR */,         -99 /* ERROR */,         -99 /* ERROR */,         -99 /* ERROR */,   -99 /* ERROR */,    -99 /* ERROR */,     -99 /* ERROR */,
            -99 /* ERROR */,         -99 /* ERROR */,         -99 /* ERROR */,           0 /* conv.ovf.i1 */,
              0 /* conv.ovf.u1 */,     0 /* conv.ovf.i2 */,     0 /* conv.ovf.u2 */,     0 /* conv.ovf.i4 */,
              0 /* conv.ovf.u4 */,     0 /* conv.ovf.i8 */,     0 /* conv.ovf.u8 */,   -99 /* ERROR */,
            -99 /* ERROR */,         -99 /* ERROR */,         -99 /* ERROR */,         -99 /* ERROR */,
            -99 /* ERROR */,         -99 /* ERROR */,           0 /* refanyval */,       0 /* ckfinite */,      -99 /* ERROR */,   -99 /* ERROR */,      0 /* mkrefany */,  -99 /* ERROR */,
            -99 /* ERROR */,         -99 /* ERROR */,         -99 /* ERROR */,         -99 /* ERROR */,         -99 /* ERROR */,   -99 /* ERROR */,    -99 /* ERROR */,     -99 /* ERROR */,
              1 /* ldtoken */,         0 /* conv.u2 */,         0 /* conv.u1 */,         0 /* conv.i */,          0 /* conv.ovf.i */,0 /* conv.ovf.u */,-1 /* add.ovf */,    -1 /* add.ovf.un */,
             -1 /* mul.ovf */,        -1 /* mul.ovf.un */,     -1 /* sub.ovf */,        -1 /* sub.ovf.un */,      0 /* endfinally */,0 /* leave */,      0 /* leave.s */,    -2 /* stind.i */,
              0 /* conv.u */};

    /// <summary>
    /// A list of the delta distances for the given CIL instructions.
    /// </summary>
    protected static readonly string[] FEopcode = {
            "arglist", "ceq", "cgt", "cgt.un", "clt", "clt.un", "ldftn", "ldvirtftn",
            "ERROR", "ldarg", "ldarga", "starg", "ldloc", "ldloca", "stloc", "localloc",
            "ERROR", "endfilter", "unaligned", "volatile", "tail", "initobj", "ERROR", "cpblk",
            "initblk", "ERROR", "rethrow", "ERROR", "sizeof", "refanytype", "readonly"};

    /// <summary>
    /// A list of the delta distances for the given FE CIL instructions.
    /// </summary>
    protected static readonly int[] FEopDeltaDistance = {
            1 /* arglist */, -1 /* ceq */, -1 /* cgt */, -1 /* cgt.un */, -1 /* clt */, -1 /* clt.un */, 1 /* ldftn */, 0 /* ldvirtftn */,
            -99 /* ERROR */, 1 /* ldarg */, 1 /* ldarga */, -1 /* starg */, 1 /* ldloc */, 1 /* ldloca */, -1 /* stloc */, 0 /* localloc */,
            -99 /* ERROR */, -1 /* endfilter */, 0 /* unaligned */, 0 /* volatile */, 0 /* tail */, -1 /* initobj */, -99 /* ERROR */, -3 /* cpblk */,
            -3 /* initblk */, -99 /* ERROR */, 0 /* rethrow */, -99 /* ERROR */, 1 /* sizeof */, 0 /* refanytype */, 0 /* readonly */};

    internal bool twoByteInstr = false;
    internal uint size = 1;
    internal uint offset, index;

    internal virtual bool Check(MetaDataOut md) {
      return false;
    }

    internal virtual void Resolve() { }

    public int GetPos() { return (int)index; }

    internal abstract string GetInstName();

    /// <summary>
    /// Get the delta distance for this instruction.
    /// </summary>
    /// <remarks>
    /// The delta distance is the resulting difference of items 
    /// left on the stack after calling this instruction.
    /// </remarks>
    /// <returns>An integer value representing the delta distance.</returns>
    internal abstract int GetDeltaDistance();

    internal virtual void BuildTables(MetaDataOut md) { }

    internal virtual void BuildCILInfo(CILWriter output) { }

    internal virtual void Write(PEWriter output) { }

    internal virtual void Write(CILWriter output) { }

  }

  /**************************************************************************/
  public class CILByte : CILInstruction {
    byte byteVal;

    /*-------------------- Constructors ---------------------------------*/

    internal CILByte(byte bVal) {
      byteVal = bVal;
    }

    public byte GetByte() { return byteVal; }

    internal override string GetInstName() {
      return Hex.Byte(byteVal);
    }

    /// <summary>
    /// Get the delta distance for this instruction.
    /// </summary>
    /// <remarks>
    /// The delta distance is the resulting difference of items 
    /// left on the stack after calling this instruction.
    /// </remarks>
    /// <returns>Zero, the delta distance for a CILByte</returns>
    internal override int GetDeltaDistance() {
      return 0;
    }

    internal override void Write(PEWriter output) {
      output.Write(byteVal);
    }

    internal override void Write(CILWriter output) {
      output.WriteLine(".emitbyte " + Hex.Byte(byteVal));  // ???? CHECK THIS ????
    }

  }

  /**************************************************************************/
  public class Instr : CILInstruction {
    protected uint instr;

    /*-------------------- Constructors ---------------------------------*/

    public Instr(Op inst) {
      instr = (uint)inst;
      if (instr >= longInstrStart) {
        instr -= longInstrStart;
        twoByteInstr = true;
        size++;
      }
    }

    internal Instr(uint inst) {
      instr = (uint)inst;
      if (instr >= longInstrStart) {
        instr -= longInstrStart;
        twoByteInstr = true;
        size++;
      }
    }

    public Op GetOp() {
      if (twoByteInstr)
        return (Op)(longInstrStart + instr);
      return (Op)instr;
    }

    internal override string GetInstName() {
      Op opInst = GetOp();
      return "" + opInst;
    }

    internal override void Write(PEWriter output) {
      //Console.WriteLine("Writing instruction " + instr + " with size " + size);
      if (twoByteInstr) output.Write(leadByte);
      output.Write((byte)instr);
    }

    internal string GetInstrString() {
      if (twoByteInstr) {
        return FEopcode[instr] + " ";
      }
      else {
        return opcode[instr] + " ";
      }
    }

    public virtual string ToString() { return this.GetInstrString(); }

    /// <summary>
    /// Get the delta distance for this instruction.
    /// </summary>
    /// <remarks>
    /// The delta distance is the resulting difference of items 
    /// left on the stack after calling this instruction.
    /// </remarks>
    /// <returns>An integer value representing the delta distance.</returns>
    internal override int GetDeltaDistance() {
      if (twoByteInstr) {
        return FEopDeltaDistance[instr];
      }
      else {
        return opDeltaDistance[instr];
      }
    }

    internal override void Write(CILWriter output) {
      if (twoByteInstr) {
        output.WriteLine(FEopcode[instr]);
      }
      else {
        output.WriteLine(opcode[instr]);
      }
    }

  }

  /**************************************************************************/
  public class IntInstr : Instr {
    int val;
    bool byteNum;

    /*-------------------- Constructors ---------------------------------*/

    public IntInstr(IntOp inst, int num)
      : base((uint)inst) {
      byteNum = inst == IntOp.ldc_i4_s;
      val = num;
      if (byteNum) size++;
      else size += 4;
    }

    public int GetInt() { return val; }
    public void SetInt(int num) { val = num; }

    internal sealed override void Write(PEWriter output) {
      base.Write(output);
      if (byteNum)
        output.Write((sbyte)val);
      else
        output.Write(val);
    }

    /// <summary>
    /// Get the delta distance for this instruction.
    /// </summary>
    /// <remarks>
    /// The delta distance is the resulting difference of items 
    /// left on the stack after calling this instruction.
    /// </remarks>
    /// <returns>An integer value representing the delta distance.</returns>
    internal override int GetDeltaDistance() {
      return opDeltaDistance[instr];
    }

    internal override void Write(CILWriter output) {
      output.WriteLine(opcode[instr] + " " + val);
    }

  }

  /**************************************************************************/
  public class UIntInstr : Instr {
    uint val;
    bool byteNum;

    /*-------------------- Constructors ---------------------------------*/

    public UIntInstr(IntOp inst, uint num)
      : base((uint)inst) {
      byteNum = (inst < IntOp.ldc_i4_s) || (inst == IntOp.unaligned);
      val = num;
      if (byteNum) size++;
      else size += 2;
    }

    public uint GetUInt() { return val; }
    public void SetUInt(uint num) { val = num; }

    /// <summary>
    /// Get the delta distance for this instruction.
    /// </summary>
    /// <remarks>
    /// The delta distance is the resulting difference of items 
    /// left on the stack after calling this instruction.
    /// </remarks>
    /// <returns>An integer value representing the delta distance.</returns>
    internal override int GetDeltaDistance() {
      if (twoByteInstr) {
        return FEopDeltaDistance[instr];
      }
      else {
        return opDeltaDistance[instr];
      }
    }

    internal sealed override void Write(PEWriter output) {
      base.Write(output);
      if (byteNum)
        output.Write((byte)val);
      else
        output.Write((ushort)val);
    }

    internal override void Write(CILWriter output) {
      if (twoByteInstr) {
        output.Write(FEopcode[instr]);
      }
      else {
        output.Write(opcode[instr]);
      }
      output.WriteLine(" " + val);
    }

  }

  /**************************************************************************/
  public class LongInstr : Instr {
    long val;

    /*-------------------- Constructors ---------------------------------*/

    public LongInstr(SpecialOp inst, long l)
      : base((uint)inst) {
      val = l;
      size += 8;
    }

    public long GetLong() { return val; }
    public void SetLong(long num) { val = num; }

    internal sealed override void Write(PEWriter output) {
      base.Write(output);
      output.Write(val);
    }

    internal override void Write(CILWriter output) {
      output.WriteLine("ldc.i8 " + val);
    }

  }

  /**************************************************************************/
  public class FloatInstr : Instr {
    float fVal;

    /*-------------------- Constructors ---------------------------------*/

    public FloatInstr(SpecialOp inst, float f)
      : base((uint)inst) {
      fVal = f;
      size += 4;
    }

    public float GetFloat() { return fVal; }
    public void SetFloat(float num) { fVal = num; }

    internal sealed override void Write(PEWriter output) {
      output.Write((byte)0x22);
      output.Write(fVal);
    }

    internal override void Write(CILWriter output) {
      output.WriteLine("ldc.r4 " + fVal);
    }

  }

  /**************************************************************************/
  public class DoubleInstr : Instr {
    double val;

    /*-------------------- Constructors ---------------------------------*/

    public DoubleInstr(SpecialOp inst, double d)
      : base((uint)inst) {
      val = d;
      size += 8;
    }

    public double GetDouble() { return val; }
    public void SetDouble(double num) { val = num; }

    internal sealed override void Write(PEWriter output) {
      base.Write(output);
      output.Write(val);
    }

    internal override void Write(CILWriter output) {
      output.WriteLine("ldc.r8 " + val);
    }

  }

  /**************************************************************************/
  public class StringInstr : Instr {
    string val;
    uint strIndex;

    /*-------------------- Constructors ---------------------------------*/

    public StringInstr(SpecialOp inst, string str)
      : base((uint)inst) {
      val = str;
      size += 4;
    }

    public string GetString() { return val; }
    public void SetString(string str) { val = str; }

    internal sealed override void BuildTables(MetaDataOut md) {
      if (Diag.DiagOn) Console.WriteLine("Adding a code string to the US heap");
      strIndex = md.AddToUSHeap(val);
    }

    internal sealed override void Write(PEWriter output) {
      base.Write(output);
      output.Write(USHeapIndex | strIndex);
    }

    internal override void Write(CILWriter output) {
      output.WriteLine("ldstr \"" + val + "\"");
    }

  }

  /**************************************************************************/
  public class CILLabel : CILInstruction {
    private static int labelNum = 0;
    private int num = -1;
    private CILInstructions buffer;

    /*-------------------- Constructors ---------------------------------*/

    public CILLabel() {
      size = 0;
    }

    internal CILLabel(uint offs) {
      size = 0;
      offset = offs;
    }

    internal uint GetLabelOffset() {
      return offset;
    }

    internal override string GetInstName() {
      return "Label" + num;
    }

    internal CILInstructions Buffer {
      get { return buffer; }
      set { buffer = value; }
    }

    internal override void BuildCILInfo(CILWriter output) {
      if (num == -1) {
        num = labelNum++;
      }
    }

    /// <summary>
    /// Get the delta distance for this instruction.
    /// </summary>
    /// <remarks>
    /// The delta distance is the resulting difference of items 
    /// left on the stack after calling this instruction.
    /// </remarks>
    /// <returns>An integer value representing the delta distance.</returns>
    internal override int GetDeltaDistance() {
      return 0;
    }

    internal override void Write(CILWriter output) {
      output.WriteLine("Label" + num + ":");
    }

    public virtual string ToString() { return "label"; }
  }

  /**************************************************************************/

  /// <summary>
  /// Abstract model for debug instructions.
  /// </summary>
  public abstract class DebugInst : CILInstruction { }

  /**************************************************************************/

  /// <summary>
  /// Defines a line instruction.
  /// </summary>
  public class Line : DebugInst {
    private static uint MaxCol = 100;
    uint startLine, startCol, endLine, endCol;
    bool hasEnd = false;
    internal SourceFile sourceFile;

    /*-------------------- Constructors ---------------------------------*/

    /// <summary>
    /// Create a new line instruction.
    /// </summary>
    /// <param name="sLine">Start of the line in the source file.</param>
    /// <param name="sCol">Starting column in the source file.</param>
    /// <param name="sFile">The filename of the souce file.</param>
    internal Line(uint sLine, uint sCol, SourceFile sFile) {
      startLine = sLine;
      startCol = sCol;
      sourceFile = sFile;
      size = 0;
    }

    /// <summary>
    /// Create a new line instruction.
    /// </summary>
    /// <param name="sLine">Start of the line in the source file.</param>
    /// <param name="sCol">Starting column in the source file.</param>
    /// <param name="eLine">Ending line in the source file.</param>
    /// <param name="eCol">Ending column in the source file.</param>
    /// <param name="sFile">The filename of the souce file.</param>
    internal Line(uint sLine, uint sCol, uint eLine, uint eCol, SourceFile sFile) {
      startLine = sLine;
      startCol = sCol;
      endLine = eLine;
      endCol = eCol;
      hasEnd = true;
      sourceFile = sFile;
      size = 0;
    }

    public int LineNum {
      get { return (int)startLine; }
    }

    internal void CalcEnd(Line next) {
      if (hasEnd) return;
      if (sourceFile != next.sourceFile) {
        endLine = startLine;
        endCol = MaxCol;
      }
      else {
        endLine = next.startLine;
        endCol = next.startCol;
        if (endCol < 0) endCol = MaxCol;
      }
      hasEnd = true;
    }

    internal void Last() {
      if (hasEnd) return;
      endLine = startLine;
      endCol = MaxCol;
      hasEnd = true;
    }

    /// <summary>
    /// Get the name of this instruction.
    /// </summary>
    /// <returns>A string with the value ".line".</returns>
    internal override string GetInstName() {
      return ".line";
    }

    /// <summary>
    /// Write this instruction to a PDB file.
    /// </summary>
    /// <param name="output">The PE writer being used to write the PE and PDB files.</param>
    internal override void Write(PEWriter output) {
      string sf = "";
      Guid doclang = Guid.Empty;
      Guid docvend = Guid.Empty;
      Guid doctype = Guid.Empty;
      if (sourceFile != null) {
        sf = sourceFile.name;
        doclang = sourceFile.language;
        docvend = sourceFile.vendor;
        doctype = sourceFile.document;

      }

      if (output.pdbWriter != null)
        output.pdbWriter.AddSequencePoint(sf, doclang, docvend, doctype, (int)offset,
            (int)startLine, (int)startCol, (int)endLine, (int)endCol);
    }

    /// <summary>
    /// Get the delta distance for this instruction.
    /// </summary>
    /// <remarks>
    /// The delta distance is the resulting difference of items 
    /// left on the stack after calling this instruction.
    /// </remarks>
    /// <returns>An integer value representing the delta distance.</returns>
    internal override int GetDeltaDistance() {
      return 0;
    }

    /// <summary>
    /// Write out a line instruction to the CIL file.
    /// </summary>
    /// <param name="output">The CIL instruction writer to use to write this instruction.</param>
    internal override void Write(CILWriter output) {
      if (output.Debug) {
        string lineDetails = startLine + ", " + startCol;
        if (hasEnd) {
          lineDetails += ", " + endLine + ", " + endCol;
          if (sourceFile != null) {
            lineDetails += ", " + sourceFile.Name;
          }
        }
        output.WriteLine(".line " + lineDetails);
      }
    }

    public virtual string ToString() { 
      return String.Format("line {0}:{1}-{2}:{3}", this.startLine, this.startCol, this.endLine, this.endCol); 
    }
  }

  /**************************************************************************/

  /// <summary>
  /// A local binding instruction that can be added to a list of CILInstructions.
  /// </summary>
  public class LocalBinding : DebugInst {
    internal int _index;
    internal string _name;
    internal DebugLocalSig _debugsig;

    /*-------------------- Constructors ---------------------------------*/

    /// <summary>
    /// Create a new local binding object.
    /// </summary>
    /// <param name="index">The index of the local in the locals tables.</param>
    /// <param name="name">The name of the local.</param>
    internal LocalBinding(int index, string name) {
      _index = index;
      _name = name;
    }

    /// <summary>
    /// The index of the local in the locals table.
    /// </summary>
    public int Index {
      get { return _index; }
    }

    /// <summary>
    /// The name of the local binding.
    /// </summary>
    public string Name {
      get { return _name; }
    }

    /// <summary>
    /// Get the delta distance for this instruction.
    /// </summary>
    /// <remarks>
    /// The delta distance is the resulting difference of items 
    /// left on the stack after calling this instruction.
    /// </remarks>
    /// <returns>An integer value representing the delta distance.</returns>
    internal override int GetDeltaDistance() {
      return 0;
    }

    /// <summary>
    /// Get the name of this instruction.
    /// </summary>
    /// <returns>A string with the name of this instruction.</returns>
    internal override string GetInstName() {
      return "debug - local binding";
    }

  }
  /**************************************************************************/

  /// <summary>
  /// Used to declare constants that exist in a given scope.
  /// </summary>
  public class ConstantBinding : DebugInst {
    private string _name;
    private object _value;
    private Type _type;
    private uint _token;

    /*-------------------- Constructors ---------------------------------*/

    /// <summary>
    /// Create a new constant binding.
    /// </summary>
    /// <param name="name">The name of the constant.</param>
    /// <param name="value">The value of the constant.</param>
    /// <param name="type">The data type of the constant.</param>
    internal ConstantBinding(string name, object value, Type type, uint token) {
      _value = value;
      _name = name;
      _type = type;
      _token = token;
    }

    /// <summary>
    /// Value of the constant.
    /// </summary>
    public object Value {
      get { return _value; }
    }

    /// <summary>
    /// The name of the constant.
    /// </summary>
    public string Name {
      get { return _name; }
    }

    /// <summary>
    /// The data type of the constant.
    /// </summary>
    public Type Type {
      get { return _type; }
    }

    /// <summary>
    /// The token for this constant.
    /// </summary>
    public uint Token {
      get { return _token; }
    }

    /// <summary>
    /// Get the type signature for this constant.
    /// </summary>
    /// <returns>A byte array of the type signature.</returns>
    public byte[] GetSig() {
      MemoryStream str = new MemoryStream();
      _type.TypeSig(str);
      return str.ToArray();
    }

    /// <summary>
    /// Get the name of this instruction.
    /// </summary>
    /// <returns>A string with the name of this instruction.</returns>
    internal override string GetInstName() {
      return "debug - constant binding";
    }

    /// <summary>
    /// Get the delta distance for this instruction.
    /// </summary>
    /// <remarks>
    /// The delta distance is the resulting difference of items 
    /// left on the stack after calling this instruction.
    /// </remarks>
    /// <returns>An integer value representing the delta distance.</returns>
    internal override int GetDeltaDistance() {
      return 0;
    }



  }

  /**************************************************************************/
  public class SwitchInstr : Instr {
    CILLabel[] cases;
    uint numCases = 0;
    int[] targets;

    /*-------------------- Constructors ---------------------------------*/

    public SwitchInstr(CILLabel[] dsts)
      : base(0x45) {
      cases = dsts;
      if (cases != null) numCases = (uint)cases.Length;
      size += 4 + (numCases * 4);
    }

    internal SwitchInstr(int[] offsets)
      : base(0x45) {
      numCases = (uint)offsets.Length;
      targets = offsets;
      size += 4 + (numCases * 4);
    }

    public CILLabel[] GetDests() { return cases; }
    public void SetDests(CILLabel[] dests) { cases = dests; }

    internal override string GetInstName() {
      return "switch";
    }

    internal void MakeTargetLabels(ArrayList labs) {
      cases = new CILLabel[numCases];
      for (int i = 0; i < numCases; i++) {
        cases[i] = CILInstructions.GetLabel(labs, (uint)(offset + size + targets[i]));
      }
    }

    internal sealed override void Write(PEWriter output) {
      base.Write(output);
      output.Write(numCases);
      for (int i = 0; i < numCases; i++) {
        int target = (int)cases[i].GetLabelOffset() - (int)(offset + size);
        output.Write(target);
      }
    }

    internal override void Write(CILWriter output) {
      throw new NotImplementedException("Switch instruction for CIL");
    }


  }


  public class Scope {
    private ArrayList _localBindings = new ArrayList();
    private ArrayList _constantBindings = new ArrayList();
    internal Scope _parent;
    internal MethodDef _thisMeth;

    internal Scope(MethodDef thisMeth)
      : this(null, thisMeth) {
    }

    internal Scope(Scope parent, MethodDef thisMeth) {
      _thisMeth = thisMeth;
      _parent = parent;
    }

    /// <summary>
    /// Add a constant to this scope.
    /// </summary>
    /// <param name="name">The name of the constant.</param>
    /// <param name="value">The value of the constant.</param>
    /// <param name="type">The type of the constant.</param>
    /// <returns>The ConstantBinding object for the new constant.</returns>
    internal ConstantBinding AddConstantBinding(string name, object value, Type type) {
      ConstantBinding binding;
      if ((binding = FindConstantBinding(name)) != null)
        return binding;

      binding = new ConstantBinding(name, value, type, _thisMeth.locToken);
      _constantBindings.Add(binding);
      return binding;
    }

    /// <summary>
    /// Find a constant in this scope.
    /// </summary>
    /// <param name="name">The name of the constant.</param>
    /// <returns>The ConstantBinding object of this constant.</returns>
    internal ConstantBinding FindConstantBinding(string name) {
      foreach (ConstantBinding binding in _constantBindings)
        if (binding.Name == name)
          return binding;
      return null;
    }

    /// <summary>
    /// Provide a complete list of all constants bound in this scope.
    /// </summary>
    public ConstantBinding[] ConstantBindings {
      get { return (ConstantBinding[])_constantBindings.ToArray(typeof(ConstantBinding)); }
    }

    internal LocalBinding AddLocalBinding(string name, int index) {
      LocalBinding binding;
      if ((binding = FindLocalBinding(name)) != null)
        return binding;

      binding = new LocalBinding(index, name);
      _localBindings.Add(binding);
      return binding;
    }

    internal LocalBinding FindLocalBinding(string name) {
      foreach (LocalBinding binding in _localBindings)
        if (binding._name == name)
          return binding;
      return null;
    }

    internal LocalBinding FindLocalBinding(int index) {
      foreach (LocalBinding binding in _localBindings)
        if (binding._index == index)
          return binding;
      return null;
    }

    public LocalBinding[] LocalBindings {
      get { return (LocalBinding[])_localBindings.ToArray(typeof(LocalBinding)); }
    }

    internal void BuildSignatures(MetaDataOut md) {
      if (!md.Debug) return;

      try {
        Local[] locals = _thisMeth.GetLocals();
        foreach (LocalBinding binding in _localBindings) {
          if (binding._debugsig == null) {
            locals[binding._index].BuildTables(md);
            binding._debugsig = md.GetDebugSig(locals[binding._index]);
          }
          binding._debugsig.BuildMDTables(md);
        }
      }
      catch (Exception e) {
        throw new Exception("Exception while writing debug info for: " +
                             this._thisMeth.NameString() + "\r\n" + e.ToString());
      }

    }

    internal void WriteLocals(PDBWriter writer) {

      try {

        Local[] locals = _thisMeth.GetLocals();

        foreach (LocalBinding binding in _localBindings) {
          writer.BindLocal(binding._name, binding._index, _thisMeth.locToken, 0, 0);
        }
      }
      catch (Exception e) {
        throw new Exception("Exception while writing debug info for: " +
            this._thisMeth.NameString() + "\r\n" + e.ToString(), e);
      }

    }
    /* Constants does not work. AKB 2007-02-03
    internal void WriteConstants(PDBWriter writer) {

        try {

            // Add each constant to the current scope
            foreach (ConstantBinding binding in _constantBindings)
                writer.BindConstant(binding);

        } catch (Exception e) {
            throw new Exception("Exception while writing debug info for: " +
                this._thisMeth.NameString() + "\r\n" + e.ToString(), e);
        }

    }
    */
  }

  /*************************************************************************/

  /// <summary>
  /// A marker instruction for when a scope should be opened in the sequence of instructions.
  /// </summary>
  public class OpenScope : DebugInst {
    internal Scope _scope;

    /// <summary>
    /// Create a new OpenScope instruction.
    /// </summary>
    /// <param name="scope">The scope that is being opened.</param>
    public OpenScope(Scope scope) {
      size = 0;
      _scope = scope;
    }

    /// <summary>
    /// Get the name for this instruction.
    /// </summary>
    /// <returns>A string with the name of the instruction.</returns>
    internal override string GetInstName() {
      return "debug - open scope";
    }

    /// <summary>
    /// Build the signatures for this instruction.
    /// </summary>
    /// <param name="md">The meta data table to write the instructions to.</param>
    internal void BuildSignatures(MetaDataOut md) {
      _scope.BuildSignatures(md);
    }

    /// <summary>
    /// Get the delta distance for this instruction.
    /// </summary>
    /// <remarks>
    /// The delta distance is the resulting difference of items 
    /// left on the stack after calling this instruction.
    /// </remarks>
    /// <returns>An integer value representing the delta distance.</returns>
    /// 
    internal override int GetDeltaDistance() {
      return 0;
    }

    /// <summary>
    /// Write this instruction to the PDB file.
    /// </summary>
    /// <param name="output">The PEWriter being used to write the PE and PDB files.</param>
    internal override void Write(PEWriter output) {
      if (output.pdbWriter != null) {
        output.pdbWriter.OpenScope((int)offset);
        _scope.WriteLocals(output.pdbWriter);
        /* Constants do not work. AKB 2007-02-03
         * _scope.WriteConstants(output.pdbWriter);
         */

      }
    }

    public virtual string ToString() { return "Open scope"; }
  }
  /************************************************************************/

  /// <summary>
  /// A marker instruction for when a scope should be closed.
  /// </summary>
  public class CloseScope : DebugInst {
    internal Scope _scope;

    /// <summary>
    /// The constructor to build a new CloseScope instruction.
    /// </summary>
    /// <param name="scope">The scope to close.</param>
    public CloseScope(Scope scope) {
      size = 0;
      _scope = scope;
    }

    /// <summary>
    /// Provide access to the name of this instruction.
    /// </summary>
    /// <returns>A string containing the name of this instruction.</returns>
    internal override string GetInstName() {
      return "debug - close scope";
    }

    /// <summary>
    /// Get the delta distance for this instruction.
    /// </summary>
    /// <remarks>
    /// The delta distance is the resulting difference of items 
    /// left on the stack after calling this instruction.
    /// </remarks>
    /// <returns>An integer value representing the delta distance.</returns>
    internal override int GetDeltaDistance() {
      return 0;
    }

    /// <summary>
    /// Write this instruction.  This instruction does not get written
    /// to the PE file.  It only applys to the PDB file.
    /// </summary>
    /// <param name="output">The PEWriter that is writing the PE file.</param>
    internal override void Write(PEWriter output) {
      if (output.pdbWriter != null)
        output.pdbWriter.CloseScope((int)offset);
    }

    public virtual string ToString() { return "Close scope"; }
  }
  /**************************************************************************/

  public class FieldInstr : Instr {
    Field field;

    /*-------------------- Constructors ---------------------------------*/

    public FieldInstr(FieldOp inst, Field f)
      : base((uint)inst) {
      field = f;
      size += 4;
    }

    public Field GetField() { return field; }

    public void SetField(Field fld) { field = fld; }

    internal override string GetInstName() {
      return "" + (FieldOp)instr;
    }

    internal sealed override void BuildTables(MetaDataOut md) {
      if (field == null) throw new InstructionException(IType.fieldOp, instr);
      if (field is FieldRef) field.BuildMDTables(md);
    }

    internal override void BuildCILInfo(CILWriter output) {
      if (field == null) throw new InstructionException(IType.fieldOp, instr);
      if (field is FieldRef) field.BuildCILInfo(output);
    }

    internal sealed override void Write(PEWriter output) {
      base.Write(output);
      output.Write(field.Token());
    }

    internal override void Write(CILWriter output) {
      output.Write(GetInstrString());
      field.WriteType(output);
      output.WriteLine();
    }


  }

  /**************************************************************************/
  public class MethInstr : Instr {
    Method meth;

    /*-------------------- Constructors ---------------------------------*/

    public MethInstr(MethodOp inst, Method m)
      : base((uint)inst) {
      meth = m;
      size += 4;
    }

    public Method GetMethod() { return meth; }

    public void SetMethod(Method mth) { meth = mth; }

    internal override string GetInstName() {
      return "" + (MethodOp)instr;
    }

    internal sealed override void BuildTables(MetaDataOut md) {
      if (meth == null)
        throw new InstructionException(IType.methOp, instr);
      if ((meth is MethodRef) || (meth is MethodSpec)) meth.BuildMDTables(md);
    }

    internal override void BuildCILInfo(CILWriter output) {
      if (meth == null) throw new InstructionException(IType.methOp, instr);
      if ((meth is MethodRef) || (meth is MethodSpec)) meth.BuildCILInfo(output);
    }

    /// <summary>
    /// Get the MethodOp this instruction represents.
    /// </summary>
    /// <returns>The method operator from the MethodOp enum.</returns>
    public MethodOp GetMethodOp() {
      return (MethodOp)instr;
    }

    /// <summary>
    /// Get the delta distance for this instruction.
    /// </summary>
    /// <remarks>
    /// The delta distance is the resulting difference of items 
    /// left on the stack after calling this instruction.
    /// </remarks>
    /// <returns>An integer value representing the delta distance.</returns>
    internal override int GetDeltaDistance() {
      switch ((MethodOp)instr) {
        case MethodOp.callvirt:
        case MethodOp.call: {

            // Add the parameter count to the depth
            int depth = (int)meth.GetSig().numPars * -1;

            // Check to see if this is an instance method
            if (meth.GetSig().HasCallConv(CallConv.Instance)) depth--;

            // Check to see if this method uses the optional parameters
            if (meth.GetSig().HasCallConv(CallConv.Vararg)) depth += (int)meth.GetSig().numOptPars * -1;

            // Check to see if this method uses the generic parameters
            if (meth.GetSig().HasCallConv(CallConv.Generic)) depth += (int)meth.GetSig().numGenPars * -1;

            // Check if a return value will be placed on the stack.
            if (!meth.GetRetType().SameType(PrimitiveType.Void)) depth++;

            return depth;
          }
        case MethodOp.newobj: {

            // Add the parameter count to the depth
            int depth = (int)meth.GetSig().numPars * -1;

            // Check to see if this method uses the optional parameters
            if (meth.GetSig().HasCallConv(CallConv.Vararg)) depth += (int)meth.GetSig().numOptPars * -1;

            // Check to see if this method uses the generic parameters
            if (meth.GetSig().HasCallConv(CallConv.Generic)) depth += (int)meth.GetSig().numGenPars * -1;

            // Add the object reference that is loaded onto the stack
            depth++;

            return depth;
          }
        case MethodOp.ldtoken:
        case MethodOp.ldftn:
          return 1;
        case MethodOp.jmp:
        case MethodOp.ldvirtfn:
          return 0;
        default:
          // Someone has added a new MethodOp and not added a case for it here.
          throw new Exception("The MethodOp for this MethoInstr is not supported.");
      }
    }

    internal sealed override void Write(PEWriter output) {
      base.Write(output);
      output.Write(meth.Token());
    }

    internal override void Write(CILWriter output) {
      output.Write(GetInstrString());
      meth.WriteType(output);
      output.WriteLine();
    }

  }

  /**************************************************************************/
  public class SigInstr : Instr {
    CalliSig signature;

    /*-------------------- Constructors ---------------------------------*/

    public SigInstr(SpecialOp inst, CalliSig sig)
      : base((uint)inst) {
      signature = sig;
      size += 4;
    }

    public CalliSig GetSig() { return signature; }

    public void SetSig(CalliSig sig) { signature = sig; }

    internal override string GetInstName() {
      return "" + (SpecialOp)instr;
    }

    /// <summary>
    /// Get the delta distance for this instruction.
    /// </summary>
    /// <remarks>
    /// The delta distance is the resulting difference of items 
    /// left on the stack after calling this instruction.
    /// </remarks>
    /// <returns>An integer value representing the delta distance.</returns>
    internal override int GetDeltaDistance() {

      // Add the parameter count to the depth
      int depth = (int)signature.NumPars * -1;

      // Check to see if this is an instance method
      if (signature.HasCallConv(CallConv.Instance)) depth--;

      // Check to see if this method uses the optional parameters
      if (signature.HasCallConv(CallConv.Vararg)) depth += (int)signature.NumOptPars * -1;

      // Check if a return value will be placed on the stack.
      if (signature.ReturnType.SameType(PrimitiveType.Void)) depth++;

      return depth;
    }

    internal sealed override void BuildTables(MetaDataOut md) {
      if (signature == null) throw new InstructionException(IType.specialOp, instr);
      signature.BuildMDTables(md);
    }

    internal override void BuildCILInfo(CILWriter output) {
      if (signature == null) throw new InstructionException(IType.specialOp, instr);
      signature.BuildCILInfo(output);
    }

    internal sealed override void Write(PEWriter output) {
      base.Write(output);
      output.Write(signature.Token());
    }

    internal override void Write(CILWriter output) {
      output.Write(GetInstrString());
      signature.Write(output);
      output.WriteLine();
    }

  }

  /**************************************************************************/
  public class TypeInstr : Instr {
    Type theType;

    /*-------------------- Constructors ---------------------------------*/

    public TypeInstr(TypeOp inst, Type aType)
      : base((uint)inst) {
      theType = aType;
      size += 4;
    }

    public Type GetTypeArg() { return theType; }

    public void SetTypeArg(Type ty) { theType = ty; }

    internal override string GetInstName() {
      return "" + (TypeOp)instr;
    }

    internal sealed override void BuildTables(MetaDataOut md) {
      if (theType == null) throw new InstructionException(IType.typeOp, instr);
      theType = theType.AddTypeSpec(md);
    }

    internal override void BuildCILInfo(CILWriter output) {
      if (theType == null) throw new InstructionException(IType.typeOp, instr);
      if (!theType.isDef()) {
        theType.BuildCILInfo(output);
      }
    }

    internal sealed override void Write(PEWriter output) {
      base.Write(output);
      output.Write(theType.Token());
    }

    internal override void Write(CILWriter output) {
      output.Write(GetInstrString());
      theType.WriteName(output);
      output.WriteLine();
    }

  }

  /**************************************************************************/
  public class BranchInstr : Instr {
    CILLabel dest;
    private bool shortVer = true;
    private static readonly byte longInstrOffset = 13;
    private int target = 0;

    /*-------------------- Constructors ---------------------------------*/

    public BranchInstr(BranchOp inst, CILLabel dst)
      : base((uint)inst) {
      dest = dst;
      shortVer = (inst < BranchOp.br) || (inst == BranchOp.leave_s);
      if (shortVer)
        size++;
      else
        size += 4;
    }

    internal BranchInstr(uint inst, int dst)
      : base(inst) {
      target = dst;
      shortVer = (inst < (uint)BranchOp.br) || (inst == (uint)BranchOp.leave_s);
      if (shortVer)
        size++;
      else
        size += 4;
    }

    public CILLabel GetDest() { return dest; }

    public void SetDest(CILLabel lab) { dest = lab; }

    /// <summary>
    /// Provide access to the branch operator
    /// </summary>
    /// <returns>The branch operator from the BranchOp enum that this instruction represents.</returns>
    public BranchOp GetBranchOp() {
      return (BranchOp)instr;
    }

    internal override string GetInstName() {
      return "" + (BranchOp)instr;
    }

    internal void MakeTargetLabel(ArrayList labs) {
      uint targetOffset = (uint)(offset + size + target);
      dest = CILInstructions.GetLabel(labs, targetOffset);
    }

    internal sealed override bool Check(MetaDataOut md) {
      target = (int)dest.GetLabelOffset() - (int)(offset + size);
      if ((target < minByteVal) || (target > maxByteVal)) { // check for longver
        if (shortVer) {
          if (instr == (uint)BranchOp.leave_s)
            instr = (uint)BranchOp.leave;
          else
            instr = instr += longInstrOffset;
          size += 3;
          shortVer = false;
          return true;
        }
      }
      else if (!shortVer) { // check for short ver
        if (instr == (uint)BranchOp.leave)
          instr = (uint)BranchOp.leave_s;
        else
          instr = instr -= longInstrOffset;
        size -= 3;
        shortVer = true;
        return true;
      }
      return false;
    }

    internal sealed override void Write(PEWriter output) {
      base.Write(output);
      if (shortVer)
        output.Write((sbyte)target);
      else
        output.Write(target);
    }

    internal override void Write(CILWriter output) {
      output.WriteLine(GetInstrString() + dest.GetInstName());
    }

  }
  /*************************************************************************/

}
