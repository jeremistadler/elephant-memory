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
            if (array1 == null || array2 == null) return false;
            if (array1.Length != array2.Length) return false;

            return array1.SequenceEqual(array2); 
        }



        const int DateLength = 15;
        const long MinDate = 635853722339839408;
        public static string FormatAsRowKey(this DateTime date)
        {
            var minified = date.ToUniversalTime().Ticks - MinDate;
            return minified.ToString().PadLeft(DateLength, '0');
        }

        public static DateTime FormatTicksAsDatetime(this string ticks)
        {
            return new DateTime(long.Parse(ticks.TrimStart('0')) + MinDate, DateTimeKind.Utc);
        }
    }
}
