using System;
using System.Runtime.InteropServices;
using Accessibility;

public static class NicedaBookmarkWindow
{
    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint id,
        ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IAccessible obj);

    [DllImport("oleacc.dll")]
    private static extern int AccessibleChildren(IAccessible obj, int start, int count,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] object[] children, out int obtained);

    private static IAccessible Get(int hwnd)
    {
        var iid = new Guid("618736e0-3c3d-11cf-810c-00aa00389b71");
        IAccessible obj;
        Marshal.ThrowExceptionForHR(AccessibleObjectFromWindow(
            new IntPtr(hwnd), 0xFFFFFFFC, ref iid, out obj));
        return obj;
    }

    private static object[] Children(IAccessible parent)
    {
        var children = new object[parent.accChildCount];
        int obtained;
        Marshal.ThrowExceptionForHR(AccessibleChildren(parent, 0, children.Length, children, out obtained));
        Array.Resize(ref children, obtained);
        return children;
    }

    public static string[] Names(int hwnd)
    {
        var parent = Get(hwnd);
        var children = Children(parent);
        var names = new string[children.Length];
        for (var i = 0; i < children.Length; i++)
        {
            var child = children[i] as IAccessible;
            names[i] = child != null ? child.get_accName(0) : parent.get_accName(children[i]);
        }
        return names;
    }

    public static void Select(int hwnd, int index)
    {
        var parent = Get(hwnd);
        var childId = Children(parent)[index];
        var child = childId as IAccessible;
        if (child != null)
            child.accSelect(3, 0);
        else
            parent.accSelect(3, childId);
    }
}
