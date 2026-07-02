namespace GoCLR.Stdlib;

using GoCLR.Runtime;

/// <summary>Small runtime helpers the lowering calls as externs (things that need
/// to inspect a value-type's internals, like a slice's nil backing array).</summary>
public static class Rt
{
    // --- named-type identity (the typed box; see GoNamed) ---------------------

    // Display name per type id, e.g. 1 -> "main.Money". Populated at startup by
    // RegisterNamedType so fmt %T and reflect can name a wrapped value.
    private static readonly System.Collections.Generic.Dictionary<long, string> _namedNames = new();
    // Reverse map so a precompiled shim can produce a typed box (GoNamed) carrying
    // the build's id for a named type it knows only by display name (e.g. the
    // encoding/json tokenizer minting "json.Delim" tokens for a Go type switch).
    private static readonly System.Collections.Generic.Dictionary<string, long> _namedIds = new();

    /// <summary>Records a named type's display name (called once per type at startup).</summary>
    public static void RegisterNamedType(long id, GoString name)
    {
        var n = name.ToDotNetString();
        _namedNames[id] = n;
        _namedIds[n] = id;
    }

    /// <summary>The build's type id for a named type known by display name, or 0.</summary>
    public static long NamedIdByName(string name) => _namedIds.TryGetValue(name, out var id) ? id : 0;

    // Runtime-allocated ids for type names first seen dynamically (reflect building a
    // "*T" pointer type at runtime). Kept above any compile-time id (which are small).
    private static long _nextDynId = 1L << 40;
    /// <summary>The id for a type display name, registering a fresh one if unseen, so a
    /// dynamically-built type name (reflect.Zero(PtrTo(t)) → "*pkg.T") has a stable
    /// identity fmt/%T and reflect.TypeOf can resolve.</summary>
    public static long InternTypeName(GoString name)
    {
        var n = name.ToDotNetString();
        lock (_namedIds)
        {
            if (_namedIds.TryGetValue(n, out var id)) return id;
            long nid = _nextDynId++;
            _namedNames[nid] = n;
            _namedIds[n] = nid;
            return nid;
        }
    }

    /// <summary>Wraps a value as a typed box for a named type known only by display
    /// name; pass-through (no box) if that name was never registered this build.</summary>
    public static object? MakeNamedByName(string name, object? value) =>
        _namedIds.TryGetValue(name, out var id) ? new GoNamed(id, value) : value;

    /// <summary>Go display name ("pkg.Type") for a type id, or "" if unknown.</summary>
    public static string NamedTypeName(long id) => _namedNames.TryGetValue(id, out var n) ? n : "";

    // Reflect/%T display name for a generic-instantiation struct, keyed by its mangled
    // CLR type name ("Pair__string_int" -> "main.Pair[string,int]").
    private static readonly System.Collections.Generic.Dictionary<string, string> _structDisplay = new();
    public static void RegisterStructDisplay(GoString clrName, GoString display) =>
        _structDisplay[clrName.ToDotNetString()] = display.ToDotNetString();
    public static string StructDisplay(string clrName) =>
        _structDisplay.TryGetValue(clrName, out var d) ? d : "";

    // Composite type id -> element/value type's identity id, for re-tagging the bare
    // elements of a []Stringer / map[K]Stringer when fmt formats them.
    private static readonly System.Collections.Generic.Dictionary<long, long> _compositeElem = new();
    /// <summary>Records a composite's element identity id (called once at startup).</summary>
    public static void RegisterCompositeElem(long compId, long elemId) => _compositeElem[compId] = elemId;
    /// <summary>The element/value identity id of a composite type id, or 0.</summary>
    public static long CompositeElemId(long compId) => _compositeElem.TryGetValue(compId, out var e) ? e : 0;

    // Named types whose underlying type is []byte (json.RawMessage, xml.CharData, …): fmt
    // formats them as a string for %s/%q/%x, like a plain []byte.
    private static readonly System.Collections.Generic.HashSet<long> _byteNamed = new();
    /// <summary>Marks a named type id as a []byte-underlying type (called once at startup).</summary>
    public static void RegisterByteNamed(long id) => _byteNamed.Add(id);
    /// <summary>Whether a named type id has a []byte underlying type.</summary>
    public static bool IsByteNamed(long id) => _byteNamed.Contains(id);

    // Map type id -> key type's identity id, for re-tagging bare map keys.
    private static readonly System.Collections.Generic.Dictionary<long, long> _compositeKey = new();
    /// <summary>Records a map's key identity id (called once at startup).</summary>
    public static void RegisterCompositeKey(long compId, long keyId) => _compositeKey[compId] = keyId;
    /// <summary>The key identity id of a map type id, or 0.</summary>
    public static long CompositeKeyId(long compId) => _compositeKey.TryGetValue(compId, out var k) ? k : 0;

    // "StructName.FieldName" -> the field type's identity id, so fmt can re-tag a bare
    // struct field value (a Stringer enum field, a precisely-typed slice/map field).
    private static readonly System.Collections.Generic.Dictionary<string, long> _fieldType = new();
    /// <summary>Records a struct field's type identity id (called once at startup).</summary>
    public static void RegisterFieldType(GoString structName, GoString field, long id)
        => _fieldType[structName.ToDotNetString() + "." + field.ToDotNetString()] = id;
    /// <summary>The identity id of a struct field's type (struct + field name), or 0.</summary>
    public static long FieldTypeId(string structName, string field)
        => _fieldType.TryGetValue(structName + "." + field, out var id) ? id : 0;

    /// <summary>Wraps a boxed value with its named-type identity for interface storage.</summary>
    public static object? MakeNamed(object? value, long id) => new GoNamed(id, value);

    /// <summary>Tags a pointer boxed into an interface for %T: a non-nil GoPtr is stamped
    /// with its pointee's identity id (only when the cell carries none, so the pointee-struct
    /// id used by dispatch is never clobbered); a nil pointer (null, which has no cell) is
    /// wrapped in GoNamed{ptrTypeId, null} — a non-nil carrier that %T names "*int" and %v
    /// renders "&lt;nil&gt;". A non-nil pointer to a nil value stays a GoPtr (never wrapped).</summary>
    public static object? TagPtrOrNil(object? p, long pointeeId, long ptrTypeId)
    {
        if (p is GoPtr gp)
        {
            if (gp.TypeId == 0) gp.TypeId = pointeeId;
            if (gp.PtrName == 0) gp.PtrName = ptrTypeId; // the pointer's own %T name
            return gp;
        }
        if (p is null) return new GoNamed(ptrTypeId, null);
        return p;
    }

    /// <summary>Unwraps a GoNamed to its underlying boxed value; pass-through otherwise.</summary>
    public static object? Unwrap(object? v) => v is GoNamed n ? n.Value : v;

    /// <summary>The named-type id of a boxed value, or 0 if it carries no identity.</summary>
    public static long NamedId(object? v) => v is GoNamed n ? n.TypeId : 0;

    /// <summary>A pointer aliasing a struct field (&amp;s.field): the getter/setter
    /// closures re-navigate the field's stable container on each *p access. typeId
    /// tags the pointee's struct type (0 for non-struct fields) so the field alias
    /// answers type assertions / pointer-receiver dispatch like any *Struct.</summary>
    public static GoPtr FieldPtr(GoClosure getter, GoClosure setter, long typeId) =>
        new() { FGet = getter, FSet = setter, TypeId = typeId };

    /// <summary>A slice is nil iff its backing array is null (Go's `s == nil`).</summary>
    public static bool SliceIsNil(GoSlice s) => s.Data == null;

    /// <summary>The nil map value: a GoMap with null Data (not a bare null), so a nil
    /// map keeps its map identity everywhere — a struct field or slice element prints
    /// "map[]" rather than &lt;nil&gt;, and the nil-map read/len/range operations work.</summary>
    public static GoMap NilMap() => new();

    /// <summary>Whether v is a nil map: either a bare null or a GoMap with null Data.</summary>
    public static bool MapIsNil(object? v) => v == null || (v is GoMap g && g.Data == null);

    /// <summary>map == nil (and, negated, !=). A map is only ever compared to nil in Go,
    /// so one side is always the nil literal; the result is whether the map side is nil,
    /// tolerant of both the null and GoMap{Data:null} representations.</summary>
    public static bool MapNilEq(object? a, object? b) => MapIsNil(a) && MapIsNil(b);

    /// <summary>Boxes a map into an interface. A nil map (null reference) becomes a
    /// non-null GoMap with null Data, so the interface is non-nil and fmt prints
    /// "map[]" — Go keeps the map type even when the map is nil (`var m map[K]V; var i
    /// any = m; i == nil` is false). A nil map is uniformly a GoMap{Data:null} value, so
    /// this only matters for a map field the CLR zero-initialized to a bare null.</summary>
    public static object BoxMap(object? m) => m ?? new GoMap();

    /// <summary>Interface equality (any == any): Go compares the dynamic VALUES for
    /// value dynamic types — any(5) == any(5) is true, strings by content, structs
    /// field-wise, typed boxes by id + value — while REFERENCE dynamic types (errors,
    /// maps, channels, closures, shim handles) keep identity semantics, exactly like
    /// Go's pointer equality (errors.New("x") != errors.New("x")). Two typed-nil
    /// pointer carriers of the same pointee type are equal (Go: (*T)(nil) ==
    /// (*T)(nil)); live pointers keep identity. x == nil stays a null check.</summary>
    public static bool IfaceEq(object? a, object? b)
    {
        if (a == null || b == null) return ReferenceEquals(a, b);
        if (ReferenceEquals(a, b)) return true;
        if (a is GoPtr pa && b is GoPtr pb)
        {
            bool nilA = pa.Value == null && pa.Arr == null && pa.FGet == null;
            bool nilB = pb.Value == null && pb.Arr == null && pb.FGet == null;
            return nilA && nilB && pa.TypeId == pb.TypeId;
        }
        var t = a.GetType();
        if (t != b.GetType()) return false;
        // Value dynamic types compare by value.
        if (a is GoString || t.IsValueType) return ValueEqual(a, b); // boxed scalars, GoString, emitted structs, slice-backed arrays
        if (a is GoNamed na && b is GoNamed nb) return na.TypeId == nb.TypeId && IfaceEq(na.Value, nb.Value);
        if (a is GoComplex ca && b is GoComplex cb) return ca.Re == cb.Re && ca.Im == cb.Im;
        // Distinct reference objects (GoError, GoMap, GoChan, GoClosure, shim handles):
        // identity, like Go's pointer equality.
        return false;
    }

    // ---- cached boxes: interface boxing of small ints/bools reuses shared immutable
    // boxes instead of allocating (boxed values are never mutated in place, and
    // equality is value-based via IfaceEq, so sharing is unobservable). The caches
    // live in GoCLR.Runtime.Boxes so runtime-side byte-slice creation shares them.
    /// <summary>//go:embed content for a []byte global: decode the compiler's
    /// base64 payload into the canonical byte slice (binary-safe).</summary>
    public static GoSlice EmbedBytes(GoString b64) =>
        Boxes.ByteSlice(System.Convert.FromBase64String(b64.ToDotNetString()));

    /// <summary>//go:embed content for a string global.</summary>
    public static GoString EmbedString(GoString b64) =>
        GoString.FromBytes(System.Convert.FromBase64String(b64.ToDotNetString()));

    /// <summary>Casts a boxed multi-result tuple back to object[] (the emitted
    /// metadata has no TypeSpec for array casts, so the cast lives here).</summary>
    public static object?[] AsTuple(object? v) => (object?[])v!;

    public static object BoxI8(long v) => Boxes.I8(v);
    public static object BoxI4(int v) => Boxes.I4(v);
    public static object BoxBool(bool b) => Boxes.Bool(b);

    /// <summary>Go value equality (==) for structs and fixed arrays: compares fields
    /// and elements recursively, matching Go's element-wise semantics rather than the
    /// reference identity of the boxed runtime objects.</summary>
    public static bool ValueEqual(object? a, object? b)
    {
        if (a == null || b == null) return ReferenceEquals(a, b);

        // Fixed arrays are slice-backed: equal length and element-wise equal.
        if (a is GoSlice sa && b is GoSlice sb)
        {
            if (sa.Len != sb.Len) return false;
            for (int i = 0; i < sa.Len; i++)
                if (!ValueEqual(sa.Data![sa.Off + i], sb.Data![sb.Off + i]))
                    return false;
            return true;
        }
        if (a is GoString ga && b is GoString gb) return GoStrings.Equal(ga, gb);

        var t = a.GetType();
        if (t != b.GetType()) return false;

        // A struct compares field by field; a pointer cell by what it points at.
        if (a is GoPtr pa && b is GoPtr pb) return ReferenceEquals(pa, pb);
        var fields = t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (fields.Length > 0 && !t.IsPrimitive && !t.IsEnum)
        {
            foreach (var f in fields)
                if (!ValueEqual(f.GetValue(a), f.GetValue(b)))
                    return false;
            return true;
        }
        // Boxed primitives/enums and other leaf values: value equality.
        return a.Equals(b);
    }

    /// <summary>Copy a fixed array's backing storage (value semantics on copy). The
    /// live element references are shallow-copied, which is correct because every
    /// element write re-boxes (it never mutates a shared boxed value in place).</summary>
    public static GoSlice ArrayClone(GoSlice s)
    {
        if (s.Data == null) return s;
        var d = new object?[s.Len];
        System.Array.Copy(s.Data, s.Off, d, 0, s.Len);
        return new GoSlice { Data = d, Off = 0, Len = s.Len, Cap = s.Len };
    }

    /// <summary>copy(dst, src): copy min(len) elements; returns the count.</summary>
    public static long Copy(GoSlice dst, GoSlice src)
    {
        if (dst.Data == null || src.Data == null) return 0;
        int n = System.Math.Min(dst.Len, src.Len);
        // Copy forward into a temp first so overlapping ranges behave like Go's copy.
        var tmp = new object?[n];
        for (int i = 0; i < n; i++) tmp[i] = src.Data[src.Off + i];
        for (int i = 0; i < n; i++) dst.Data[dst.Off + i] = tmp[i];
        return n;
    }

    /// <summary>copy(dst []byte, src string): copy the string's UTF-8 bytes.</summary>
    public static long CopyString(GoSlice dst, GoString src)
    {
        if (dst.Data == null) return 0;
        byte[] b = src.Bytes;
        int n = System.Math.Min(dst.Len, b.Length);
        for (int i = 0; i < n; i++) dst.Data[dst.Off + i] = Boxes.I4(b[i]);
        return n;
    }

    /// <summary>clear(m): remove all entries from a map.</summary>
    public static void ClearMap(GoMap? m) => m?.Data?.Clear();

    /// <summary>clear(s): set every live element of a slice to its zero value.</summary>
    public static void ClearSlice(GoSlice s, object? zero)
    {
        if (s.Data == null) return;
        for (int i = 0; i < s.Len; i++) s.Data[s.Off + i] = zero;
    }

    /// <summary>&amp;s[i] — a pointer aliasing element i of the slice's backing array,
    /// so the slice and the pointer share storage.</summary>
    // PtrPointeeKind classifies a GoPtr's pointee representation (1=string, 2=slice,
    // 3=int64, 4=int32, 5=bool, 6=float64, 7=uint64, 8=uint32, 9=float32; 0=other).
    // Lets a type assertion/switch distinguish *int64 / *string / *[]byte, which share
    // the one GoPtr cell type — the discrimination database/sql's Scan depends on.
    public static long PtrPointeeKind(object? p) => GoPtrs.PointeeKind(p);

    // IsShimKind reports whether v is the opaque shim value of the given Go type. An
    // opaque shim type lowers to System.Object, so `isinst` alone would match every boxed
    // value — including a plain int64, which is how a type switch `case time.Time` wrongly
    // captured an integer. Primitives are excluded here; the concrete shim class is matched
    // through the self-declared [GoShim] registry (see ShimTypes), so the discrimination
    // is data-driven and agnostic — no Go type is hardcoded.
    public static bool IsShimKind(object? v, GoString goName)
    {
        switch (v)
        {
            case null:
            case long: case int: case ulong: case uint:
            case double: case float: case bool:
            case GoString: case GoSlice: case GoMap: case GoPtr: case GoClosure:
                return false;
        }
        return ShimTypes.Is(v!, goName.ToDotNetString());
    }

    // IsShimKindStrict is IsShimKind without the "any non-primitive" heuristic: it is true
    // only when v's CLR class is the registered [GoShim] for goName. Interface dispatch
    // uses this so an unannotated value can never be mis-routed to a shim method.
    public static bool IsShimKindStrict(object? v, GoString goName)
    {
        switch (v)
        {
            case null:
            case long: case int: case ulong: case uint:
            case double: case float: case bool:
            case GoString: case GoSlice: case GoMap: case GoPtr: case GoClosure:
                return false;
        }
        return ShimTypes.IsStrict(v!, goName.ToDotNetString());
    }

    // FatalPanic reports an uncaught panic in Go's shape (panic: <value> + a goroutine
    // header) instead of the .NET unhandled-exception dump, then exits with status 2 — so a
    // crash reads like Go, not like an alien CLR stack. The .NET frames are kept under the
    // goroutine header for debugging (goclr has no Go-format stack metadata). Recovered
    // panics never reach here (they unwind through the deferred-recover path).
    public static void FatalPanic(object? ex)
    {
        var p = ex as GoPanicException ?? new GoPanicException(ex);
        var sb = new System.Text.StringBuilder();
        sb.Append(p.Message); // "panic: <value>"
        sb.Append("\n\ngoroutine 1 [running]:\n");
        var st = p.StackTrace;
        if (!string.IsNullOrEmpty(st)) sb.Append(st).Append('\n');
        System.Console.Out.Flush();
        System.Console.Error.Write(sb.ToString());
        System.Console.Error.Flush();
        System.Environment.Exit(2);
    }

    // BoxNilPtr boxes a concrete pointer into an interface. A non-nil pointer is returned
    // as-is; a nil pointer becomes a non-null GoPtr carrying the pointee's type id, so the
    // interface is non-nil — Go keeps the dynamic type even when the pointer is nil
    // (`var p *T; var i any = p; i == nil` is false). Method dispatch / type assertion then
    // resolve through the GoPtr's id exactly as for a non-nil pointer (a nil receiver).
    public static object BoxNilPtr(GoPtr? p, long typeId) => (object?)p ?? GoPtrs.New(null, typeId);

    public static GoPtr ElemAddr(GoSlice s, long i)
    {
        if (s.Data == null || i < 0 || i >= s.Len)
            throw new GoPanicException(GoString.FromDotNetString(
                $"runtime error: index out of range [{i}] with length {s.Len}"));
        return new GoPtr { Arr = s.Data, Idx = (int)(s.Off + i) };
    }

    /// <summary>&amp;s[i] for a struct-element slice: the alias pointer carries the
    /// pointee's type id so interface dispatch and type assertions through the
    /// element pointer resolve (like Rt.FieldPtr does for field aliases).</summary>
    public static GoPtr ElemAddrT(GoSlice s, long i, long typeId)
    {
        var p = ElemAddr(s, i);
        p.TypeId = typeId;
        return p;
    }

    /// <summary>The nil slice value (zero value of every slice type): a GoSlice with
    /// a null backing array, so `s == nil` is true and append still works.</summary>
    public static GoSlice NilSlice() => default;

    /// <summary>append(s, other...) — append all of other's elements.</summary>
    public static GoSlice AppendSlice(GoSlice s, GoSlice other)
    {
        int m = other.Len;
        if (m == 0) return s;
        // Snapshot first: other may alias s's backing array (append(s, s...)), and the
        // bulk append below writes into s in place when the result fits in cap.
        var add = new object?[m];
        if (other.Data != null) System.Array.Copy(other.Data, other.Off, add, 0, m);
        return GoSlices.AppendN(s, add, m);
    }

    /// <summary>append(b, str...) where b is []byte — append the string's bytes.</summary>
    public static GoSlice AppendString(GoSlice s, GoString str)
    {
        var bytes = str.Bytes;
        int m = bytes.Length;
        if (m == 0) return s;
        var add = new object?[m];
        for (int i = 0; i < m; i++) add[i] = Boxes.I4(bytes[i]);
        return GoSlices.AppendN(s, add, m);
    }

    /// <summary>s[lo:hi:max] — the full-slice expression (caps the result's capacity).</summary>
    public static GoSlice Slice3(GoSlice s, long lo, long hi, long max) => GoSlices.Slice3(s, lo, hi, max);

    /// <summary>s[low:high] for a string — the byte subrange (Go slices strings by
    /// byte offset). Panics out of range, like Go.</summary>
    public static GoString StrSlice(GoString s, long low, long high)
    {
        byte[] b = s.Bytes;
        // Go reports a string's slice bound past its length as "[:hi] with length len".
        if (high > b.Length) throw StrBounds($"[:{high}] with length {b.Length}");
        if (low < 0) throw StrBounds($"[{low}:]");
        if (high < low) throw StrBounds($"[{low}:{high}]");
        var r = new byte[high - low];
        System.Array.Copy(b, (int)low, r, 0, (int)(high - low));
        return GoString.FromBytes(r);
    }

    private static GoPanicException StrBounds(string detail) =>
        new(GoString.FromDotNetString("runtime error: slice bounds out of range " + detail));

    /// <summary>A failed single-value type assertion x.(T): panics like Go with
    /// "interface conversion: &lt;iface&gt; is &lt;actual&gt;, not &lt;asserted&gt;" instead of
    /// letting the CLR throw a raw InvalidCastException.</summary>
    public static void AssertFail(object? v, GoString ifaceName, GoString asserted)
    {
        // go/types spells the empty interface "interface{}"; Go's runtime/reflect use
        // "interface {}" (with a space), so normalize to match the panic text exactly.
        static string Sp(string s) => s.Replace("interface{}", "interface {}");
        string actual = v == null ? "nil" : Sp(Fmt.TypeName(v));
        throw new GoPanicException(GoString.FromDotNetString(
            $"interface conversion: {Sp(ifaceName.ToDotNetString())} is {actual}, not {Sp(asserted.ToDotNetString())}"));
    }
}
