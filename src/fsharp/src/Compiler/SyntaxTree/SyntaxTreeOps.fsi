// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

module internal FSharp.Compiler.SyntaxTreeOps

open FSharp.Compiler.Features
open FSharp.Compiler.Text
open FSharp.Compiler.Xml
open FSharp.Compiler.Syntax
open FSharp.Compiler.SyntaxTrivia

[<Class>]
type SynArgNameGenerator =
    new: unit -> SynArgNameGenerator
    member New: unit -> string
    member Reset: unit -> unit

val ident: s: string * r: range -> Ident

val textOfId: id: Ident -> string

val pathOfLid: lid: Ident list -> string list

val arrPathOfLid: lid: Ident list -> string[]

val textOfPath: path: seq<string> -> string

val textOfLid: lid: Ident list -> string

val rangeOfLid: lid: Ident list -> range

val mkSynId: m: range -> s: string -> Ident

val pathToSynLid: m: range -> p: string list -> Ident list

val mkSynIdGet: m: range -> n: string -> SynExpr

val mkSynLidGet: m: range -> path: string list -> n: string -> SynExpr

val mkSynIdGetWithAlt: m: range -> id: Ident -> altInfo: SynSimplePatAlternativeIdInfo ref option -> SynExpr

val mkSynSimplePatVar: isOpt: bool -> id: Ident -> SynSimplePat

val mkSynCompGenSimplePatVar: id: Ident -> SynSimplePat

val pushUnaryArg: expr: SynExpr -> arg: Ident -> SynExpr

val inline findSynAttribute: attrName: string -> synAttrs: SynAttributes -> bool

/// Match a long identifier, including the case for single identifiers which gets a more optimized node in the syntax tree.
[<return: Struct>]
val (|LongOrSingleIdent|_|):
    inp: SynExpr -> (bool * SynLongIdent * SynSimplePatAlternativeIdInfo ref option * range) voption

[<return: Struct>]
val (|SingleIdent|_|): inp: SynExpr -> Ident voption

/// This affects placement of debug points
val IsControlFlowExpression: e: SynExpr -> bool

// The debug point for a 'let' extends to include the 'let' if we're not defining a function and the r.h.s. is not a control-flow
// expression. Otherwise, there is no debug point at the binding.
val IsDebugPointBinding: synPat: SynPat -> synExpr: SynExpr -> bool

val mkSynAnonField: ty: SynType * xmlDoc: PreXmlDoc -> SynField

val mkSynNamedField: ident: Ident * ty: SynType * xmlDoc: PreXmlDoc * m: range -> SynField

val mkSynPatVar: vis: SynAccess option -> id: Ident -> SynPat

val mkSynThisPatVar: id: Ident -> SynPat

val mkSynPatMaybeVar: lidwd: SynLongIdent -> vis: SynAccess option -> m: range -> SynPat

[<return: Struct>]
val (|SynPatForConstructorDecl|_|): x: SynPat -> SynPat voption

/// Recognize the '()' in 'new()'
[<return: Struct>]
val (|SynPatForNullaryArgs|_|): x: SynPat -> unit voption

val (|SynExprErrorSkip|): p: SynExpr -> SynExpr

[<return: Struct>]
val (|SynExprParen|_|): e: SynExpr -> (SynExpr * range * range option * range) voption

val (|SynPatErrorSkip|): p: SynPat -> SynPat

/// Push non-simple parts of a patten match over onto the r.h.s. of a lambda.
/// Return a simple pattern and a function to build a match on the r.h.s. if the pattern is complex
val SimplePatOfPat: synArgNameGenerator: SynArgNameGenerator -> p: SynPat -> SynSimplePat * (SynExpr -> SynExpr) option

val appFunOpt: funOpt: ('a -> 'a) option -> x: 'a -> 'a

val composeFunOpt: funOpt1: ('a -> 'a) option -> funOpt2: ('a -> 'a) option -> ('a -> 'a) option

val SimplePatsOfPat:
    synArgNameGenerator: SynArgNameGenerator -> p: SynPat -> SynSimplePats * (SynExpr -> SynExpr) option

val PushPatternToExpr:
    synArgNameGenerator: SynArgNameGenerator -> isMember: bool -> pat: SynPat -> rhs: SynExpr -> SynSimplePats * SynExpr

/// "fun (UnionCase x) (UnionCase y) -> body"
///       ==>
///   "fun tmp1 tmp2 ->
///        let (UnionCase x) = tmp1 in
///        let (UnionCase y) = tmp2 in
///        body"
val PushCurriedPatternsToExpr:
    synArgNameGenerator: SynArgNameGenerator ->
    wholem: range ->
    isMember: bool ->
    pats: SynPat list ->
    arrow: Range option ->
    rhs: SynExpr ->
        SynSimplePats list * SynExpr

val opNameParenGet: string

val opNameQMark: string

val mkSynOperator: opm: range -> oper: string -> SynExpr

val mkSynInfix: opm: range -> l: SynExpr -> oper: string -> r: SynExpr -> SynExpr

val mkSynBifix: m: range -> oper: string -> x1: SynExpr -> x2: SynExpr -> SynExpr

val mkSynTrifix: m: range -> oper: string -> x1: SynExpr -> x2: SynExpr -> x3: SynExpr -> SynExpr

val mkSynPrefixPrim: opm: range -> m: range -> oper: string -> x: SynExpr -> SynExpr

val mkSynPrefix: opm: range -> m: range -> oper: string -> x: SynExpr -> SynExpr

val mkSynCaseName: m: range -> n: string -> Ident list

val mkSynApp1: f: SynExpr -> x1: SynExpr -> m: range -> SynExpr

val mkSynApp2: f: SynExpr -> x1: SynExpr -> x2: SynExpr -> m: range -> SynExpr

val mkSynApp3: f: SynExpr -> x1: SynExpr -> x2: SynExpr -> x3: SynExpr -> m: range -> SynExpr

val mkSynApp4: f: SynExpr -> x1: SynExpr -> x2: SynExpr -> x3: SynExpr -> x4: SynExpr -> m: range -> SynExpr

val mkSynApp5:
    f: SynExpr -> x1: SynExpr -> x2: SynExpr -> x3: SynExpr -> x4: SynExpr -> x5: SynExpr -> m: range -> SynExpr

val mkSynDotParenSet: m: range -> a: SynExpr -> b: SynExpr -> c: SynExpr -> SynExpr

val mkSynDotBrackGet: m: range -> mDot: range -> a: SynExpr -> b: SynExpr -> SynExpr

val mkSynQMarkSet: m: range -> a: SynExpr -> b: SynExpr -> c: SynExpr -> SynExpr

//val mkSynDotBrackSliceGet: m:range -> mDot:range -> arr:SynExpr -> sliceArg:SynIndexerArg -> SynExpr

//val mkSynDotBrackSeqSliceGet: m:range -> mDot:range -> arr:SynExpr -> argsList:SynIndexerArg list -> SynExpr

val mkSynDotParenGet: mLhs: range -> mDot: range -> a: SynExpr -> b: SynExpr -> SynExpr

val mkSynUnit: m: range -> SynExpr

val mkSynUnitPat: m: range -> SynPat

val mkSynDelay: m: range -> e: SynExpr -> SynExpr

val mkSynAssign: l: SynExpr -> r: SynExpr -> SynExpr

val mkSynDot: mDot: range -> m: range -> l: SynExpr -> r: SynIdent -> SynExpr

val mkSynDotMissing: mDot: range -> m: range -> expr: SynExpr -> SynExpr

val mkSynFunMatchLambdas:
    synArgNameGenerator: SynArgNameGenerator ->
    isMember: bool ->
    wholem: range ->
    ps: SynPat list ->
    arrow: Range option ->
    e: SynExpr ->
        SynExpr

val arbExpr: debugStr: string * range: range -> SynExpr

val unionRangeWithListBy: projectRangeFromThing: ('a -> range) -> m: range -> listOfThing: 'a list -> range

val inline unionRangeWithXmlDoc: xmlDoc: PreXmlDoc -> range: range -> range

val mkAttributeList: attrs: SynAttribute list -> range: range -> SynAttributeList list

val ConcatAttributesLists: attrsLists: SynAttributeList list -> SynAttribute list

val (|Attributes|): synAttributes: SynAttributeList list -> SynAttribute list

val (|TyparDecls|): typarDecls: SynTyparDecls option -> SynTyparDecl list
val (|TyparsAndConstraints|): typarDecls: SynTyparDecls option -> SynTyparDecl list * SynTypeConstraint list
val (|ValTyparDecls|): valTyparDecls: SynValTyparDecls -> SynTyparDecl list * SynTypeConstraint list * bool

val rangeOfNonNilAttrs: attrs: SynAttributes -> range

val stripParenTypes: synType: SynType -> SynType

val (|StripParenTypes|): synType: SynType -> SynType

/// Operations related to the syntactic analysis of arguments of value, function and member definitions and signatures.
module SynInfo =
    /// The argument information for an argument without a name
    val unnamedTopArg1: SynArgInfo

    /// The argument information for a curried argument without a name
    val unnamedTopArg: SynArgInfo list

    /// The argument information for a '()' argument
    val unitArgData: SynArgInfo list

    /// The 'argument' information for a return value where no attributes are given for the return value (the normal case)
    val unnamedRetVal: SynArgInfo

    /// The 'argument' information for the 'this'/'self' parameter in the cases where it is not given explicitly
    val selfMetadata: SynArgInfo list

    /// Determine if a syntactic information represents a member without arguments (which is implicitly a property getter)
    val HasNoArgs: SynValInfo -> bool

    /// Check if one particular argument is an optional argument. Used when adjusting the
    /// types of optional arguments for function and member signatures.
    val IsOptionalArg: SynArgInfo -> bool

    /// Check if there are any optional arguments in the syntactic argument information. Used when adjusting the
    /// types of optional arguments for function and member signatures.
    val HasOptionalArgs: SynValInfo -> bool

    /// Add a parameter entry to the syntactic value information to represent the '()' argument to a property getter. This is
    /// used for the implicit '()' argument in property getter signature specifications.
    val IncorporateEmptyTupledArgForPropertyGetter: SynValInfo -> SynValInfo

    /// Add a parameter entry to the syntactic value information to represent the 'this' argument. This is
    /// used for the implicit 'this' argument in member signature specifications.
    val IncorporateSelfArg: SynValInfo -> SynValInfo

    /// Add a parameter entry to the syntactic value information to represent the value argument for a property setter. This is
    /// used for the implicit value argument in property setter signature specifications.
    val IncorporateSetterArg: SynValInfo -> SynValInfo

    /// Get the argument counts for each curried argument group. Used in some adhoc places in tc.fs.
    val AritiesOfArgs: SynValInfo -> int list

    /// Get the argument attributes from the syntactic information for an argument.
    val AttribsOfArgData: SynArgInfo -> SynAttribute list

    /// Infer the syntactic argument info for a single argument from a simple pattern.
    val InferSynArgInfoFromSimplePat: attribs: SynAttributes -> p: SynSimplePat -> SynArgInfo

    /// Infer the syntactic argument info for one or more arguments one or more simple patterns.
    val InferSynArgInfoFromSimplePats: x: SynSimplePats -> SynArgInfo list

    /// Infer the syntactic argument info for one or more arguments a pattern.
    val InferSynArgInfoFromPat: p: SynPat -> SynArgInfo list

    /// Make sure only a solitary unit argument has unit elimination
    val AdjustArgsForUnitElimination: infosForArgs: SynArgInfo list list -> SynArgInfo list list

    /// Transform a property declared using '[static] member P = expr' to a method taking a "unit" argument.
    /// This is similar to IncorporateEmptyTupledArgForPropertyGetter, but applies to member definitions
    /// rather than member signatures.
    val AdjustMemberArgs: memFlags: SynMemberKind -> infosForArgs: 'a list list -> 'a list list

    val InferSynReturnData: retInfo: SynReturnInfo option -> SynArgInfo

    val emptySynValData: SynValData

    val emptySynArgInfo: SynArgInfo

    /// Infer the syntactic information for a 'let' or 'member' definition, based on the argument pattern,
    /// any declared return information (e.g. .NET attributes on the return element), and the r.h.s. expression
    /// in the case of 'let' definitions.
    val InferSynValData:
        memberFlagsOpt: SynMemberFlags option *
        pat: SynPat option *
        retInfo: SynReturnInfo option *
        origRhsExpr: SynExpr ->
            SynValData

val mkSynBindingRhs:
    staticOptimizations: (SynStaticOptimizationConstraint list * SynExpr) list ->
    rhsExpr: SynExpr ->
    mRhs: range ->
    retInfo: (range option * SynReturnInfo) option ->
        SynExpr * SynBindingReturnInfo option

val mkSynBinding:
    xmlDoc: PreXmlDoc * headPat: SynPat ->
        vis: SynAccess option *
        isInline: bool *
        isMutable: bool *
        mBind: range *
        spBind: DebugPointAtBinding *
        retInfo: (range option * SynReturnInfo) option *
        origRhsExpr: SynExpr *
        mRhs: range *
        staticOptimizations: (SynStaticOptimizationConstraint list * SynExpr) list *
        attrs: SynAttributes *
        memberFlagsOpt: SynMemberFlags option *
        trivia: SynBindingTrivia ->
            SynBinding

val NonVirtualMemberFlags: k: SynMemberKind -> SynMemberFlags

val CtorMemberFlags: SynMemberFlags

val ClassCtorMemberFlags: SynMemberFlags

val OverrideMemberFlags: k: SynMemberKind -> SynMemberFlags

val AbstractMemberFlags: isInstance: bool -> k: SynMemberKind -> SynMemberFlags

val StaticMemberFlags: k: SynMemberKind -> SynMemberFlags

val ImplementStaticMemberFlags: k: SynMemberKind -> SynMemberFlags

val inferredTyparDecls: SynValTyparDecls

val noInferredTypars: SynValTyparDecls

val unionBindingAndMembers: bindings: SynBinding list -> members: SynMemberDefn list -> SynBinding list

val synExprContainsError: inpExpr: SynExpr -> bool

val parsedHashDirectiveArguments: ParsedHashDirectiveArgument list -> LanguageVersion -> string list

val parsedHashDirectiveArgumentsNoCheck: ParsedHashDirectiveArgument list -> string list

val parsedHashDirectiveStringArguments: ParsedHashDirectiveArgument list -> LanguageVersion -> string list

/// 'e1 && e2'
[<return: Struct>]
val (|SynAndAlso|_|): SynExpr -> (SynExpr * SynExpr) voption

/// 'e1 || e2'
[<return: Struct>]
val (|SynOrElse|_|): SynExpr -> (SynExpr * SynExpr) voption

/// 'e1 |> e2'
[<return: Struct>]
val (|SynPipeRight|_|): SynExpr -> (SynExpr * SynExpr) voption

/// 'e1 ||> e2'
[<return: Struct>]
val (|SynPipeRight2|_|): SynExpr -> (SynExpr * SynExpr * SynExpr) voption

/// 'e1 |||> e2'
[<return: Struct>]
val (|SynPipeRight3|_|): SynExpr -> (SynExpr * SynExpr * SynExpr * SynExpr) voption

val prependIdentInLongIdentWithTrivia: ident: SynIdent -> mDot: range -> lid: SynLongIdent -> SynLongIdent

val mkDynamicArgExpr: expr: SynExpr -> SynExpr

val normalizeTuplePat: pats: SynPat list -> commas: range list -> SynPat list * range List

val desugarGetSetMembers: memberDefns: SynMemberDefns -> SynMemberDefns

val getTypeFromTuplePath: path: SynTupleTypeSegment list -> SynType list

[<return: Struct>]
val (|MultiDimensionArrayType|_|): t: SynType -> (int * SynType * range) voption

val (|TypesForTypar|): t: SynType -> SynType list

/// Generated get_XYZ or set_XYZ ident text
[<return: Struct>]
val (|Get_OrSet_Ident|_|): Ident -> unit voption

val getGetterSetterAccess:
    SynValSigAccess -> SynMemberKind -> Features.LanguageVersion -> SynAccess option * SynAccess option

/// Adds SynPat.Or pattern for unfinished empty clause above
val addEmptyMatchClause: mBar1: range -> mBar2: range -> clauses: SynMatchClause list -> SynMatchClause list
