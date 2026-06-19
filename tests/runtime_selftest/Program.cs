using GoCLR.Runtime;

// A framework-free smoke test for the GoCLR runtime core. Exits non-zero on the
// first failed invariant. These assertions mirror Go semantics that the spec
// calls out as success criteria (UTF-8 strings, panic/recover, slices, maps).

int failures = 0;
void Check(string name, bool cond)
{
    Console.WriteLine($"[{(cond ? "ok  " : "FAIL")}] {name}");
    if (!cond) failures++;
}

// --- GoString UTF-8 semantics (spec §27, criterion #9) ---
// "á€z" = C3 A1 | E2 82 AC | 7A  => 6 bytes, 3 runes, s[0] == 195
var s = GoString.FromDotNetString("á€z");
Check("len(s) counts bytes == 6", s.Len == 6);
Check("s[0] is a byte == 195", s.ByteAt(0) == 195);
var runes = s.ToRunes();
Check("[]rune(s) has 3 runes", runes.Length == 3);
Check("runes == [225, 8364, 122]", runes[0] == 225 && runes[1] == 8364 && runes[2] == 122);
Check("roundtrip to .NET string", s.ToDotNetString() == "á€z");
Check("string compare is lexicographic over bytes",
    GoString.Compare(GoString.FromDotNetString("a"), GoString.FromDotNetString("b")) < 0);

// --- Slices ---
var sl = GoSlice<long>.Make(0, 2);
sl = GoSlice<long>.Append(sl, 1, 2, 3); // forces growth past cap
Check("append grows length to 3", sl.Len == 3);
Check("slice indexing", sl[0] == 1 && sl[2] == 3);
var sub = sl.Slice(1, 3);
Check("reslice shares backing", sub.Len == 2 && sub[0] == 2);
Check("nil slice has len 0", GoSlice<long>.Nil.Len == 0 && GoSlice<long>.Nil.IsNil);

// --- Maps ---
var m = GoMap<GoString, long>.Make();
m.Set(GoString.FromDotNetString("k"), 42);
var (v, ok) = m.Get2(GoString.FromDotNetString("k"));
Check("map get2 hit", ok && v == 42);
var (_, ok2) = m.Get2(GoString.FromDotNetString("missing"));
Check("map get2 miss", !ok2);
bool nilMapPanicked = false;
try { GoMap<GoString, long>.Nil.Set(GoString.FromDotNetString("x"), 1); }
catch (GoPanicException) { nilMapPanicked = true; }
Check("assignment to nil map panics", nilMapPanicked);

// --- defer / panic / recover (spec §15, criterion #7) ---
// Emulates: func() (recovered any) { defer func(){ recovered = recover() }(); panic("boom") }
object? RecoverDemo()
{
    var ctx = GoRuntime.Current();
    var defers = new DeferStack();
    object? recovered = null;
    defers.Push(() => { recovered = ctx.Recover(); });
    try
    {
        Builtins.Panic(GoString.FromDotNetString("boom"));
    }
    catch (GoPanicException)
    {
        // The lowering's try/finally would call RunAll in the finally; here we
        // invoke it explicitly to model that.
    }
    defers.RunAll(ctx);
    return recovered;
}
var rec = RecoverDemo();
Check("recover() returns the panic value", rec is GoString g && g.ToDotNetString() == "boom");

// --- Channels ---
var ch = GoChan<long>.Make(2);
ch.Send(10);
ch.Send(20);
ch.Close();
long sum = 0;
foreach (var x in ch.Range()) sum += x;
Check("buffered channel send/range/close", sum == 30);
bool sendClosedPanicked = false;
try { var c2 = GoChan<long>.Make(1); c2.Close(); c2.Send(1); }
catch (GoPanicException) { sendClosedPanicked = true; }
Check("send on closed channel panics", sendClosedPanicked);

// --- Interfaces / type descriptors ---
var userType = new GoTypeDescriptor("User", "example/pkg", GoKind.Struct);
var iface = GoInterface.Of(userType, "data");
Check("interface holding value is not nil", !iface.IsNil);
Check("nil interface is nil", GoInterface.Nil.IsNil);
var (data, hit) = iface.Assert(userType);
Check("type assertion by identity", hit && (string?)data == "data");

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILURE(S)");
return failures == 0 ? 0 : 1;
