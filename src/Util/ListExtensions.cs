using System.Collections.Generic;
using System.Linq;

namespace Genelib.Extensions {
    public static class ListExtensions {
        // Based on
        // https://stackoverflow.com/questions/12231569/is-there-in-c-sharp-a-method-for-listt-like-resize-in-c-for-vectort
        public static void EnsureSize<T>(this List<T> list, int size, T t) {
            if (size > list.Count) {
                list.EnsureCapacity(size);
                list.AddRange(Enumerable.Repeat(t, size - list.Count));
            }
        }

        public static void EnsureSize<T>(this List<T> list, int size) where T : new() {
            EnsureSize(list, size, new T());
        }
    }
}
