namespace POGLib.Core
{
    /// <summary>
    /// Which way round a triangle of an OBJ goes once its Z is negated (D17). Unity draws the side a triangle's
    /// <c>cross(b - a, c - a)</c> points to. An OBJ's triangles run the other way round after the mirror, so a file as
    /// every modelling tool writes it must be turned over; until 0.1.2 it was not, and such a model was drawn inside
    /// out. A file written the wrong way round (the sample's berry was) has normals that say so: where a face has
    /// normals, they decide. No Unity types, so the unit tests compile it.
    /// </summary>
    internal static class ObjWinding
    {
        /// <param name="a">corners, already converted to Unity (Z negated), as x, y, z</param>
        /// <param name="n">the corners' normals added up, converted the same way; null when the face names none</param>
        public static bool MustTurn(float[] a, float[] b, float[] c, float[] n)
        {
            if (n != null)
            {
                float ux = b[0] - a[0], uy = b[1] - a[1], uz = b[2] - a[2];
                float wx = c[0] - a[0], wy = c[1] - a[1], wz = c[2] - a[2];
                float dot = (uy * wz - uz * wy) * n[0] + (uz * wx - ux * wz) * n[1] + (ux * wy - uy * wx) * n[2];
                if (dot != 0f) return dot < 0f;
            }
            return true;
        }
    }
}
