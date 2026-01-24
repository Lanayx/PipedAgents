namespace PipedAgents.Strands

open Fable.Core
open Fable.Core.JsInterop

[<AllowNullLiteral>]
type ZodType =
    abstract describe: string -> ZodType
    abstract optional: unit -> ZodType
    abstract ``default``: obj -> ZodType

[<AllowNullLiteral>]
type ZodString =
    inherit ZodType
    abstract email: unit -> ZodString
    abstract url: unit -> ZodString
    abstract uuid: unit -> ZodString

[<AllowNullLiteral>]
type ZodNumber =
    inherit ZodType
    abstract min: float -> ZodNumber
    abstract max: float -> ZodNumber
    abstract int: unit -> ZodNumber

[<AllowNullLiteral>]
type ZodBoolean =
    inherit ZodType

[<AllowNullLiteral>]
type ZodArray =
    inherit ZodType
    abstract min: int -> ZodArray
    abstract max: int -> ZodArray
    abstract length: int -> ZodArray

[<AllowNullLiteral>]
type ZodObject =
    inherit ZodType
    abstract strict: unit -> ZodObject
    abstract passthrough: unit -> ZodObject

[<AllowNullLiteral>]
type IZod =
    abstract string: unit -> ZodString
    abstract number: unit -> ZodNumber
    abstract boolean: unit -> ZodBoolean
    abstract object: shape: obj -> ZodObject
    abstract array: schema: ZodType -> ZodArray
    abstract enum: values: string array -> ZodType
    abstract union: schemas: ZodType array -> ZodType
    abstract intersection: left: ZodType * right: ZodType -> ZodType
    abstract any: unit -> ZodType
    abstract null': unit -> ZodType
    abstract undefined: unit -> ZodType
    abstract literal: value: obj -> ZodType

[<AutoOpen>]
module ZodBindings =
    [<Import("z", "zod")>]
    let z : IZod = jsNative

    [<Import("zodToJsonSchema", "zod-to-json-schema")>]
    let zodToJsonSchema (schema: ZodType) : obj = jsNative
