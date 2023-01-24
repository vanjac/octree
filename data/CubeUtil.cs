using Godot;
using System;

public static class CubeUtil {
    /// <summary>
    /// Rotate a 3-bit branch child index left. Each bit corresponds to an axis (1 = positive).
    /// </summary>
    /// <param name="index">The 3-bit index.</param>
    /// <param name="count">Number of bits left to rotate.</param>
    /// <returns>The index, with its bits rearranged.</returns>
    public static int CycleIndex(int index, int count) {
        return ((index & 1) << (count % 3))
            | (((index >> 1) & 1) << ((count + 1) % 3))
            | (((index >> 2) & 1) << ((count + 2) % 3));
    }

    /// <summary>
    /// Rearrange the components of a vector in the same way that CycleIndex() rearranges bits.
    /// Eg. cycling a vector by 1 will move the X component to Y, Y to Z, and Z to X.
    /// </summary>
    /// <param name="vec">Vector to cycle.</param>
    /// <param name="count">Number of positions to move each component.</param>
    /// <returns>The vector with its components rearranged.</returns>
    public static Vector3 CycleVector(Vector3 vec, int count) {
        Vector3 newVec = new Vector3();
        newVec[count % 3] = vec.x;
        newVec[(count + 1) % 3] = vec.y;
        newVec[(count + 2) % 3] = vec.z;
        return newVec;
    }

    /// <summary>
    /// Construct a vector with each component determined by a bit in the index (0 or 1).
    /// Used to determine the position of a child within its parent branch.
    /// </summary>
    /// <param name="index">3-bit child index whose bits are used to build the vector.</param>
    /// <returns>A vector (bit 0, bit 1, bit 2).</returns>
    public static Vector3 IndexVector(int index) {
        return new Vector3(index & 1, (index >> 1) & 1, (index >> 2) & 1);
    }

    /// <summary>
    /// Find the size of a cube at the given depth in the world.
    /// </summary>
    /// <param name="depth">Depth of the cube in the world.</param>
    /// <returns>World-space size of cube</returns>
    public static float WorldCubeSize(int depth) {
        depth -= CubeWorld.UNIT_DEPTH;
        if (depth >= 0)
            return 1f / (1 << depth);
        else
            return (float)(1 << -depth);
    }

    /// <summary>
    /// Determine the cube face selected by the user.
    /// </summary>
    /// <param name="pos">Raycast collision point (unit position).</param>
    /// <param name="normal">Raycast collision normal.</param>
    /// <param name="depth">Depth in world of cubes to select.</param>
    /// <param name="rootPos">Position of world root cube (for rounding).</param>
    /// <param name="axis">Axis of the face that was selected.</param>
    /// <param name="dir">
    /// True if the selected face's normal is in the positive direction, false if negative.
    /// </param>
    /// <returns>CubePos in world of the face that was selected.</returns>
    public static CubePos PickFace(Vector3 pos, Vector3 normal, int depth, CubePos rootPos,
            out int axis, out bool dir) {
        var absNormal = normal.Abs();
        axis = (int)absNormal.MaxAxis();
        dir = normal[axis] >= 0;
        // TODO selecting half size cube??
        return (CubePos.FromWorldPos(pos + absNormal.Round() * CubeUtil.WorldCubeSize(depth + 1))
            - rootPos).Floor(depth) + rootPos;
    }
}

/// <summary>
/// A 3D point inside a cube (always within its bounds).
/// Each coordinate is a 32-bit unsigned integer. The entire range is used, so 0 is the minimum
/// coordinate and 2^32 (not representable) is the maximum.
/// </summary>
public struct CubePos {
    public static readonly CubePos ZERO = CubePos.FromChildIndex(0);
    public static readonly CubePos HALF = CubePos.FromChildIndex(7);
    private uint x, y, z;

    public CubePos(uint x, uint y, uint z) {
        this.x = x; this.y = y; this.z = z;
    }

    public CubePos(uint all) {
        this.x = this.y = this.z = all;
    }

    private CubePos(Vector3 v) {
        this.x = (uint)(long)v.x; this.y = (uint)(long)v.y; this.z = (uint)(long)v.z;
    }

    public uint this[int i] {
        readonly get {
            switch (i % 3) {
                case 0: return x;
                case 1: return y;
                case 2: return z;
                default: throw new IndexOutOfRangeException();
            }
        }
        set {
            switch (i % 3) {
                case 0: x = value; break;
                case 1: y = value; break;
                case 2: z = value; break;
                default: throw new IndexOutOfRangeException();
            }
        }
    }

    /// <summary>
    /// Get the size of a cube in the range of CubePos coordinates.
    /// </summary>
    /// <param name="depth">Depth of the cube relative to root.</param>
    /// <returns>Size of the cube relative to root.</returns>
    public static uint CubeSize(int depth) {
        return 1u << (32 - depth);
    }

    /// <summary>
    /// Get the position of a child within the unit cube.
    /// </summary>
    /// <param name="i">Child index.</param>
    /// <returns>Position of the child assuming a depth of 0.</returns>
    public static CubePos FromChildIndex(int i) {
        return new CubePos(((uint)i & 1) << 31, ((uint)i & 2) << 30, ((uint)i & 4) << 29);
    }

    /// <summary>
    /// Build a CubePos by setting a single axis to a value, and all others to 0.
    /// </summary>
    /// <param name="axis">Axis index to be set.</param>
    /// <param name="len">Axis value to set.</param>
    /// <param name="dir">If false, value will be made negative (2's complement).</param>
    /// <returns>A CubePos with a single axis set.</returns>
    public static CubePos FromAxis(int axis, uint len = 1, bool dir = true) {
        CubePos pos = ZERO;
        pos[axis] = dir ? len : (uint)-len;
        return pos;
    }

    /// <summary>
    /// Convert world-space vector to CubePos. Origin is at CubePos.HALF, and 1 unit is the size of
    /// a cube with depth CubeWorld.UNIT_DEPTH.
    /// </summary>
    /// <param name="pos">Vector in world space.</param>
    /// <returns>CubePos in world space.</returns>
    public static CubePos FromWorldPos(Vector3 pos) {
        return new CubePos(pos * (1 << (32 - CubeWorld.UNIT_DEPTH))) + CubePos.HALF;
    }

    /// <summary>
    /// Convert to world-space vector. Reverse of FromWorldPos().
    /// </summary>
    /// <returns>Vector in world space.</returns>
    public readonly Vector3 ToWorldPos() {
        return (this - CubePos.HALF).ToWorldSize();
    }

    /// <summary>
    /// Convert signed box dimensions (measured from 0) to world space vector.
    /// </summary>
    /// <returns>Box dimensions in world space.</returns>
    public readonly Vector3 ToWorldSize() {
        return ToVector3Signed() / (1 << (32 - CubeWorld.UNIT_DEPTH));
    }

    private readonly Vector3 ToVector3Signed() {
        return new Vector3((int)x, (int)y, (int)z);
    }

    /// <summary>
    /// Convert world-space cube position to root space.
    /// </summary>
    /// <param name="world">World to use for transformation</param>
    /// <returns>Cube position in root coordinates</returns>
    public readonly CubePos ToRoot(CubeWorld world) {
        return (this - world.rootPos) << world.rootDepth;
    }

    /// <summary>
    /// Round each coordinate down to intervals the size of a descendant cube.
    /// </summary>
    /// <param name="depth">Depth of the descendent cube.</param>
    /// <returns>CubePos where each coordinate is some multiple of CubeSize(depth).</returns>
    public readonly CubePos Floor(int depth) {
        if (depth <= 0)
            return ZERO;
        return this & new CubePos(~0u << (32 - depth));
    }

    /// <summary>
    /// Get the index of the child of a branch that would contain this CubePos.
    /// </summary>
    /// <returns>Child index (0-7).</returns>
    public readonly int ChildIndex() {
        return (int)(((x >> 31) & 1) | ((y >> 30) & 2) | ((z >> 29) & 4));
    }

    /// <summary>
    /// Check if the point is inside a cube. (Inclusive of minimum boundary, exclusive of maximum).
    /// </summary>
    /// <param name="origin">Minimum position of the cube.</param>
    /// <param name="depth">Depth of the cube (size found by CubeSize(depth)).</param>
    /// <returns>true if the point is inside the cube.</returns>
    public readonly bool InCube(CubePos origin, int depth) {
        uint size = CubeSize(depth);
        return x >= origin.x && x < origin.x + size
            && y >= origin.y && y < origin.y + size
            && z >= origin.z && z < origin.z + size;
    }

    public readonly override string ToString() {
        return $"<{x:X8}, {y:X8}, {z:X8}>";
    }

    public static CubePos operator +(CubePos a, CubePos b)
        => new CubePos(a.x + b.x, a.y + b.y, a.z + b.z);
    public static CubePos operator -(CubePos a, CubePos b)
        => new CubePos(a.x - b.x, a.y - b.y, a.z - b.z);
    public static CubePos operator |(CubePos a, CubePos b)
        => new CubePos(a.x | b.x, a.y | b.y, a.z | b.z);
    public static CubePos operator &(CubePos a, CubePos b)
        => new CubePos(a.x & b.x, a.y & b.y, a.z & b.z);
    public static CubePos operator >>(CubePos a, int b)
        => new CubePos(a.x >> b, a.y >> b, a.z >> b);
    public static CubePos operator <<(CubePos a, int b)
        => new CubePos(a.x << b, a.y << b, a.z << b);
}
