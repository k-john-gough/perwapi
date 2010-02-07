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
    /**************************************************************************/

    // Various Enumerations for PEFiles

    /// <summary>
    /// flags for the assembly (.corflags)
    /// </summary>
    public enum CorFlags
    {
        /// <summary>
        /// IL only
        /// </summary>
        CF_IL_ONLY = 1,
        /// <summary>
        /// 32 bits
        /// </summary>
        CF_32_BITREQUIRED = 2,
        /// <summary>
        /// strong name signed
        /// </summary>
        CF_STRONGNAMESIGNED = 8,
        /// <summary>
        /// track debug data
        /// </summary>
        CF_TRACKDEBUGDATA = 0x10000
    }

    /// <summary>
    /// subsystem for the assembly (.subsystem)
    /// </summary>
    public enum SubSystem
    {
        /// <summary>
        /// native subsystem
        /// </summary>
        Native = 1,
        /// <summary>
        /// gui app
        /// </summary>
        Windows_GUI = 2,
        /// <summary>
        /// console app
        /// </summary>
        Windows_CUI = 3,
        /// <summary>
        /// os2 console
        /// </summary>
        OS2_CUI = 5,
        /// <summary>
        /// posix console
        /// </summary>
        POSIX_CUI = 7,
        /// <summary>
        /// native windows
        /// </summary>
        Native_Windows = 8,
        /// <summary>
        /// CE gui
        /// </summary>
        Windows_CE_GUI = 9
    }

    /// <summary>
    /// Hash algorithms for the assembly
    /// </summary>
    public enum HashAlgorithmType
    {
        /// <summary>
        /// No hash algorithm
        /// </summary>
        None,
        /// <summary>
        /// SHA1
        /// </summary>
        SHA1 = 0x8004
    }

    /// <summary>
    /// Attributes for this assembly
    /// </summary>
    public enum AssemAttr
    {
        /// <summary>
        /// Public key assembly attribute
        /// </summary>
        PublicKey = 0x01,
        /// <summary>
        /// retargetable assembly
        /// </summary>
        Retargetable = 0x100,
        /// <summary>
        /// JIT tracking
        /// </summary>
        EnableJITCompileTracking = 0x8000,
        /// <summary>
        /// Disable JIT compile optimizer
        /// </summary>
        DisableJITCompileOptimizer = 0x4000
    }

    /// <summary>
    /// Method call conventions
    /// </summary>
    [FlagsAttribute]
    public enum CallConv
    {
        /// <summary>
        /// default cc
        /// </summary>
        Default,
        /// <summary>
        /// cdecl
        /// </summary>
        Cdecl,
        /// <summary>
        /// stdcall
        /// </summary>
        Stdcall,
        /// <summary>
        /// this call
        /// </summary>
        Thiscall,
        /// <summary>
        /// fast call
        /// </summary>
        Fastcall,
        /// <summary>
        /// var arg
        /// </summary>
        Vararg,
        /// <summary>
        /// generic
        /// </summary>
        Generic = 0x10,
        /// <summary>
        /// instance
        /// </summary>
        Instance = 0x20,
        /// <summary>
        /// explicit instance
        /// </summary>
        InstanceExplicit = 0x60
    }

    /// <summary>
    /// Method Types for Events and Properties
    /// </summary>
    public enum MethodType
    {
        /// <summary>
        /// setter
        /// </summary>
        Setter = 0x01,
        /// <summary>
        /// getter
        /// </summary>
        Getter,
        /// <summary>
        /// other
        /// </summary>
        Other = 0x04,
        /// <summary>
        /// add on
        /// </summary>
        AddOn = 0x08,
        /// <summary>
        /// remove on
        /// </summary>
        RemoveOn = 0x10,
        /// <summary>
        /// Fire
        /// </summary>
        Fire = 0x20
    }

    /// <summary>
    /// Type custom modifier
    /// </summary>
    public enum CustomModifier
    {
        /// <summary>
        /// mod req
        /// </summary>
        modreq = 0x1F,
        /// <summary>
        /// mod opt
        /// </summary>
        modopt
    };

    /// <summary>
    /// Attibutes for a class
    /// </summary>
    [FlagsAttribute]
    public enum TypeAttr
    {
        Private, Public, NestedPublic, NestedPrivate,
        NestedFamily, NestedAssembly, NestedFamAndAssem, NestedFamOrAssem,
        SequentialLayout, ExplicitLayout = 0x10, Interface = 0x20,
        Abstract = 0x80, PublicAbstract = 0x81, Sealed = 0x100,
        PublicSealed = 0x101, SpecialName = 0x400, RTSpecialName = 0x800,
        Import = 0x1000, Serializable = 0x2000, UnicodeClass = 0x10000,
        AutoClass = 0x20000, BeforeFieldInit = 0x100000
    }

    /// <summary>
    /// Attributes for a field
    /// </summary>
    [FlagsAttribute]
    public enum FieldAttr
    {
        Default, Private, FamAndAssem, Assembly,
        Family, FamOrAssem, Public, Static = 0x10, PublicStatic = 0x16,
        Initonly = 0x20, Literal = 0x40, Notserialized = 0x80,
        SpecialName = 0x200, RTSpecialName = 0x400
    }

    /// <summary>
    /// Attributes for a method
    /// </summary>
    [FlagsAttribute]
    public enum MethAttr
    {
        Default, Private, FamAndAssem, Assembly,
        Family, FamOrAssem, Public, Static = 0x0010, PublicStatic = 0x16,
        Final = 0x0020, PublicStaticFinal = 0x36, Virtual = 0x0040,
        PrivateVirtual, PublicVirtual = 0x0046, HideBySig = 0x0080,
        NewSlot = 0x0100, Abstract = 0x0400, SpecialName = 0x0800,
        RTSpecialName = 0x1000, SpecialRTSpecialName = 0x1800,
        RequireSecObject = 0x8000
    }

    /// <summary>
    /// Attributes for .pinvokeimpl method declarations
    /// </summary>
    [FlagsAttribute]
    public enum PInvokeAttr
    {
        ansi = 2, unicode = 4, autochar = 6,
        lasterr = 0x040, winapi = 0x100, cdecl = 0x200, stdcall = 0x300,
        thiscall = 0x400, fastcall = 0x500
    }

    /// <summary>
    /// Implementation attributes for a method
    /// </summary>
    [FlagsAttribute]
    public enum ImplAttr
    {
        IL, Native, OPTIL, Runtime, Unmanaged,
        ForwardRef = 0x10, PreserveSig = 0x0080, InternalCall = 0x1000,
        Synchronised = 0x0020, Synchronized = 0x0020, NoInLining = 0x0008
    }

    /// <summary>
    /// Modes for a parameter
    /// </summary>
    [FlagsAttribute]
    public enum ParamAttr { Default, In, Out, Opt = 4 }

    /// <summary>
    /// Flags for a generic parameter
    /// </summary>
    [Flags]
    public enum GenericParamAttr
    {
        NonVariant,
        Covariant,
        Contravariant,
        ReferenceType = 0x4,
        RequireDefaultCtor = 0x10
    }

    /// <summary>
    /// Which version of PE file to build
    /// </summary>
    public enum NetVersion
    {
        Everett,   /* version 1.1.4322  */
        Whidbey40, /* version 2.0.40607 beta 1*/
        Whidbey41, /* version 2.0.41202 */
        Whidbey50, /* version 2.0.50215 beta2*/
        Version2,  /* version 2.0.50727.0 */
        V2_Compact /* version 2.0.0.0 compact framework */
    }

    /// <summary>
    /// CIL instructions
    /// </summary>
    public enum Op
    {
        nop, breakOp, ldarg_0, ldarg_1, ldarg_2, ldarg_3, ldloc_0, ldloc_1, ldloc_2,
        ldloc_3, stloc_0, stloc_1, stloc_2, stloc_3,
        ldnull = 0x14, ldc_i4_m1, ldc_i4_0, ldc_i4_1, ldc_i4_2, ldc_i4_3,
        ldc_i4_4, ldc_i4_5, ldc_i4_6, ldc_i4_7, ldc_i4_8, dup = 0x25, pop,
        ret = 0x2A, ldind_i1 = 0x46, ldind_u1, ldind_i2, ldind_u2, ldind_i4,
        ldind_u4, ldind_i8, ldind_i, ldind_r4, ldind_r8, ldind_ref, stind_ref,
        stind_i1, stind_i2, stind_i4, stind_i8, stind_r4, stind_r8, add, sub, mul,
        div, div_un, rem, rem_un, and, or, xor, shl, shr, shr_un, neg, not,
        conv_i1, conv_i2, conv_i4, conv_i8, conv_r4, conv_r8, conv_u4, conv_u8,
        conv_r_un = 0x76, throwOp = 0x7A, conv_ovf_i1_un = 0x82, conv_ovf_i2_un,
        conv_ovf_i4_un, conv_ovf_i8_un, conf_ovf_u1_un, conv_ovf_u2_un,
        conv_ovf_u4_un, conv_ovf_u8_un, conv_ovf_i_un, conv_ovf_u_un,
        ldlen = 0x8E, ldelem_i1 = 0x90, ldelem_u1, ldelem_i2, ldelem_u2,
        ldelem_i4, ldelem_u4, ldelem_i8, ldelem_i, ldelem_r4, ldelem_r8,
        ldelem_ref, stelem_i, stelem_i1, stelem_i2, stelem_i4, stelem_i8,
        stelem_r4, stelem_r8, stelem_ref, conv_ovf_i1 = 0xb3, conv_ovf_u1,
        conv_ovf_i2, conv_ovf_u2, conv_ovf_i4, conv_ovf_u4, conv_ovf_i8,
        conv_ovf_u8, ckfinite = 0xC3, conv_u2 = 0xD1, conv_u1, conv_i,
        conv_ovf_i, conv_ovf_u, add_ovf, add_ovf_un, mul_ovf, mul_ovf_un,
        sub_ovf, sub_ovf_un, endfinally, stind_i = 0xDF, conv_u,
        arglist = 0xFE00, ceq, cgt, cgt_un, clt, clt_un, localloc = 0xFE0F,
        endfilter = 0xFE11, volatile_ = 0xFE13, tail_, cpblk = 0xFE17, initblk,
        rethrow = 0xFE1A, refanytype = 0xFE1D, readOnly
    }

    /// <summary>
    /// CIL instructions requiring an integer parameter
    /// </summary>
    public enum IntOp
    {
        ldarg_s = 0x0E, ldarga_s, starg_s, ldloc_s, ldloca_s,
        stloc_s, ldc_i4_s = 0x1F, ldc_i4, ldarg = 0xFE09,
        ldarga, starg, ldloc, ldloca, stloc, unaligned = 0xFE12
    }

    /// <summary>
    /// CIL instructions requiring a field parameter
    /// </summary>
    public enum FieldOp
    {
        ldfld = 0x7B, ldflda, stfld, ldsfld, ldsflda,
        stsfld, ldtoken = 0xD0
    }

    /// <summary>
    /// CIL instructions requiring a method parameter
    /// </summary>
    public enum MethodOp
    {
        jmp = 0x27, call, callvirt = 0x6F, newobj = 0x73,
        ldtoken = 0xD0, ldftn = 0xFE06, ldvirtfn
    }

    /// <summary>
    /// CIL instructions requiring a type parameter
    /// </summary>
    public enum TypeOp
    {
        cpobj = 0x70, ldobj, castclass = 0x74, isinst,
        unbox = 0x79, stobj = 0x81, box = 0x8C, newarr,
        ldelema = 0x8F, ldelem_any = 0xA3, stelem_any, unbox_any,
        refanyval = 0xC2, mkrefany = 0xC6,
        ldtoken = 0xD0, initobj = 0xFE15, constrained, sizeOf = 0xFE1C
    }

    /// <summary>
    /// CIL branch instructions
    /// </summary>
    public enum BranchOp
    {
        br_s = 0x2B, brfalse_s, brtrue_s, beq_s, bge_s, bgt_s, ble_s,
        blt_s, bne_un_s, bge_un_s, bgt_un_s, ble_un_s, blt_un_s,
        br, brfalse, brtrue, beq, bge, bgt, ble, blt, bne_un, bge_un, bgt_un, ble_un, blt_un,
        leave = 0xDD, leave_s
    }

    public enum SpecialOp
    {
        ldc_i8 = 0x21, ldc_r4, ldc_r8, calli = 0x29,
        Switch = 0x45, ldstr = 0x72
    }

    /// <summary>
    /// Index for all the tables in the meta data
    /// </summary>
    public enum MDTable
    {
        Module, TypeRef, TypeDef, Field = 0x04, Method = 0x06,
        Param = 0x08, InterfaceImpl, MemberRef, Constant, CustomAttribute,
        FieldMarshal, DeclSecurity, ClassLayout, FieldLayout, StandAloneSig,
        EventMap, Event = 0x14, PropertyMap, Property = 0x17, MethodSemantics,
        MethodImpl, ModuleRef, TypeSpec, ImplMap, FieldRVA, Assembly = 0x20,
        AssemblyProcessor, AssemblyOS, AssemblyRef, AssemblyRefProcessor,
        AssemblyRefOS, File, ExportedType, ManifestResource, NestedClass,
        GenericParam, MethodSpec, GenericParamConstraint, MaxMDTable
    }

    public enum NativeTypeIx
    {
        Void = 0x01, Boolean, I1, U1, I2, U2, I4, U4,
        I8, U8, R4, R8, SysChar, Variant, Currency, Ptr, Decimal, Date, BStr,
        LPStr, LPWStr, LPTStr, FixedSysString, ObjectRef, IUnknown, IDispatch,
        Struct, Intf, SafeArray, FixedArray, Int, UInt, NestedStruct, ByValStr,
        AnsiBStr, TBStr, VariantBool, Func, AsAny = 0x28, Array = 0x2A, LPStruct,
        CustomMarshaller, Error
    }

    public enum SafeArrayType
    {
        int16 = 2, int32, float32, float64,
        currency, date, bstr, dispatch, error, boolean, variant, unknown,
        Decimal, int8 = 16, uint8, uint16, uint32, Int = 22, UInt,
        record = 0x24,
        MAX = 0x50
    }

    internal enum CIx
    {
        TypeDefOrRef, HasConstant, HasCustomAttr, HasFieldMarshal,
        HasDeclSecurity, MemberRefParent, HasSemantics, MethodDefOrRef,
        MemberForwarded, Implementation, CustomAttributeType, ResolutionScope,
        TypeOrMethodDef, MaxCIx
    }

    internal enum MapType { eventMap, propertyMap, nestedClass }

    public enum ElementType : byte
    {
        End, Void, Boolean, Char, I1, U1, I2, U2, I4, U4,
        I8, U8, R4, R8, String, Ptr, ByRef, ValueType, Class, Var, Array, GenericInst,
        TypedByRef, I = 0x18, U, FnPtr = 0x1B, Object, SZArray, MVar, CmodReqd,
        CmodOpt, Internal, Modifier = 0x40, Sentinel, Pinned = 0x45, ClassType = 0x50
    }

    public enum SecurityAction
    {
        Request = 0x01, Demand, Assert, Deny, PermitOnly,
        LinkDemand, InheritanceDemand, RequestMinimum, RequestOptional, RequestRefuse,
        PreJITGrant, PreJITDeny, NonCASDemand, NonCASLinkDemand, NonCASInheritanceDemand
    }

    internal enum IType
    {
        op, methOp, fieldOp, typeOp, specialOp, int8Op, uint8Op, uint16Op,
        int32Op, branchOp
    }

}