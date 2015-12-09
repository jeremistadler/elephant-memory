﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reflection
{
    public static class Extentions
    {
        public static bool NotEmpty(this string text)
        {
            return !string.IsNullOrEmpty(text);
        }

        public static bool NotEmpty<T>(this IEnumerable<T> items)
        {
            return items != null && items.Any();
        }

        public static bool ArraysEquals<T>(this T[] array1, T[] array2)
        {
            if (array1 == array2) return true;
            if (array1 == null || array2 == null)
                return false;

            return array1.SequenceEqual(array2); 
        }

        public static string Reverse(this string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }
    }
}
