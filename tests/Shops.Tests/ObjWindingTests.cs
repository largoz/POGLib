using POGLib.Core;
using Xunit;

// D17: an OBJ's triangle, its Z negated, must end up with its outside where the file's normal points.
public class ObjWindingTests
{
    // A triangle in the file's XY plane, seen from +Z: a, b, c run counter-clockwise (what every tool writes for a face
    // whose outside is +Z). Converted: Z negated, so the outside is -Z.
    static readonly float[] A = { 0, 0, 0 }, B = { 1, 0, 0 }, C = { 0, 1, 0 };
    static readonly float[] Outside = { 0, 0, -3 };

    static float[] Cross(float[] a, float[] b, float[] c)
    {
        float ux = b[0] - a[0], uy = b[1] - a[1], uz = b[2] - a[2], wx = c[0] - a[0], wy = c[1] - a[1], wz = c[2] - a[2];
        return new[] { uy * wz - uz * wy, uz * wx - ux * wz, ux * wy - uy * wx };
    }

    static float[] Drawn(float[] a, float[] b, float[] c, float[] n) =>
        ObjWinding.MustTurn(a, b, c, n) ? Cross(a, c, b) : Cross(a, b, c);

    [Fact] public void AFaceAsToolsWriteItIsDrawnOutside() => Assert.True(Drawn(A, B, C, Outside)[2] < 0);

    [Fact] public void AFaceWrittenTheWrongWayRoundIsDrawnOutsideToo() => Assert.True(Drawn(A, C, B, Outside)[2] < 0);

    [Fact] public void WithoutNormalsTheFileIsTakenAsToolsWriteIt() => Assert.True(Drawn(A, B, C, null)[2] < 0);

    [Fact] public void ANormalThatSaysNothingIsTakenAsNone()
    {
        Assert.True(ObjWinding.MustTurn(A, B, C, new float[] { 1, 0, 0 }));   // along the face
        Assert.True(ObjWinding.MustTurn(A, A, C, Outside));                   // no area
    }
}
