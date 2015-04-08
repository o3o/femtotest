using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FemtoTest {
   // Introducing "The Asserts" - all pretty self explanatory
   [SkipInStackTrace]
   public static partial class Assert {
      public static void Throw(bool Condition, Func<string> message) {
         if (!Condition)
            throw new AssertionException(message());
      }
      public static void IsTrue(bool test) {
         Throw(test, () => "Expression is not true");
      }

      public static void IsFalse(bool test) {
         Throw(!test, () => "Expression is not false");
      }

      public static void AreSame(object a, object b) {
         Throw(object.ReferenceEquals(a, b), () => "Object references are not the same");
      }

      public static void AreNotSame(object a, object b) {
         Throw(!object.ReferenceEquals(a, b), () => "Object references are the same");
      }

      private static bool TestEqual(object a, object b) {
         if (a == null && b == null)
            return true;
         if (a == null || b == null)
            return false;
         return Object.Equals(a, b);
      }

      private static void AreEqual(object a, object b, Func<bool> Compare) {
         Throw(Compare(), () => string.Format("Objects are not equal\n  lhs: {0}\n  rhs: {1}", Utils.FormatValue(a), Utils.FormatValue(b)));
      }

      private static void AreNotEqual(object a, object b, Func<bool> Compare) {
         Throw(!Compare(), () => string.Format("Objects are not equal\n  lhs: {0}\n  rhs: {1}", Utils.FormatValue(a), Utils.FormatValue(b)));
      }

      public static void AreEqual(object a, object b) {
         AreEqual(a, b, () => TestEqual(a, b));
      }

      public static void AreNotEqual(object a, object b) {
         AreNotEqual(a, b, () => TestEqual(a, b));
      }

      public static void AreEqual(double a, double b, double within) {
         AreEqual(a, b, () => Math.Abs(a - b) < within);
      }

      public static void AreNotEqual(double a, double b, double within) {
         AreNotEqual(a, b, () => Math.Abs(a - b) < within);
      }

      public static void AreEqual<T>(T a, T b) {
         AreEqual(a, b, () => Object.Equals(a, b));
      }

      public static void AreNotEqual<T>(T a, T b) {
         AreNotEqual(a, b, () => Object.Equals(a, b));
      }

      public static void AreEqual(string a, string b, bool ignoreCase = false) {
         Throw(string.Compare(a, b, ignoreCase) == 0, () => {
            var offset = Utils.CountCommonPrefix(a, b, ignoreCase);
            var xa = Utils.FormatValue(Utils.GetStringExtract(a, offset));
            var xb = Utils.FormatValue(Utils.GetStringExtract(b, offset));
            return string.Format("Strings are not equal at offset {0}\n  lhs: {1}\n  rhs: {2}\n{3}^", offset, xa, xb, new string(' ', Utils.CountCommonPrefix(xa, xb, ignoreCase) + 7));
         });
      }

      public static void AreEqual(string a, string b) {
         AreEqual(a, b, false);
      }

      public static void AreNotEqual(string a, string b, bool ignoreCase = false) {
         Throw(string.Compare(a, b, ignoreCase) != 0, () => string.Format("Strings are not equal\n  lhs: {0}\n  rhs: {1}", Utils.FormatValue(a), Utils.FormatValue(b)));
      }

      public static void IsEmpty(string val) {
         Throw(val != null && val.Length == 0, () => string.Format("String is not empty: {0}", Utils.FormatValue(val)));
      }

      public static void IsNotEmpty(string val) {
         Throw(val != null && val.Length != 0, () => "String is empty");
      }

      public static void IsNullOrEmpty(string val) {
         Throw(string.IsNullOrEmpty(val), () => string.Format("String is not empty: {0}", Utils.FormatValue(val)));
      }

      public static void IsNotNullOrEmpty(string val) {
         Throw(!string.IsNullOrEmpty(val), () => string.Format("String is not empty: {0}", Utils.FormatValue(val)));
      }

      public static void IsEmpty(System.Collections.IEnumerable collection) {
         Throw(collection != null && collection.Cast<object>().Count() == 0, () => string.Format("Collection is not empty\n  Items: {0}", Utils.FormatValue(collection)));
      }

      public static void IsNotEmpty(System.Collections.IEnumerable collection) {
         Throw(collection != null && collection.Cast<object>().Count() != 0, () => "Collection is empty");
      }

      public static void Contains(System.Collections.IEnumerable collection, object item) {
         Throw(collection.Cast<object>().Contains(item), () => string.Format("Collection doesn't contain {0}\n  Items: {1}", Utils.FormatValue(item), Utils.FormatValue(collection)));
      }

      public static void DoesNotContain(System.Collections.IEnumerable collection, object item) {
         Throw(!collection.Cast<object>().Contains(item), () => string.Format("Collection does contain {0}", Utils.FormatValue(item)));
      }

      public static void Contains(string str, string contains, bool ignoreCase) {
         Throw(str.IndexOf(contains, ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture) >= 0,
               () => string.Format("String doesn't contain substring\n  expected: {0}\n  found:    {1}", Utils.FormatValue(contains), Utils.FormatValue(str)));
      }

      public static void Contains(string str, string contains) {
         Contains(str, contains, false);
      }

      public static void DoesNotContain(string str, string contains, bool ignoreCase = false) {
         Throw(str.IndexOf(contains, ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture) < 0,
               () => string.Format("String does contain substring\n  didn't expect: {0}\n  found:         {1}", Utils.FormatValue(contains), Utils.FormatValue(str)));
      }

      public static void Matches(string str, string regex, RegexOptions options = RegexOptions.None) {
         Throw(new Regex(regex, options).IsMatch(str), () => string.Format("String doesn't match expression\n  regex: \"{0}\"\n  found: {1}", regex, Utils.FormatValue(str)));
      }

      public static void DoesNotMatch(string str, string regex, RegexOptions options = RegexOptions.None) {
         Throw(!(new Regex(regex, options).IsMatch(str)), () => string.Format("String matches expression\n  regex: \"{0}\"\n  found: {1}", regex, Utils.FormatValue(str)));
      }

      public static void IsNull(object val) {
         Throw(val == null, () => string.Format("Object reference is not null - {0}", Utils.FormatValue(val)));
      }
      public static void IsNotNull(object val) {
         Throw(val != null, () => "Object reference is null");
      }
      public static void Compare(object a, object b, Func<int, bool> Check, string comparison) {
         Throw(Check((a as IComparable).CompareTo(b)), () => string.Format("Comparison failed: {0} {1} {2}", Utils.FormatValue(a), comparison, Utils.FormatValue(b)));
      }

      public static void Greater<T>(T a, T b) {
         Compare(a, b, r => r > 0, ">");
      }

      public static void GreaterOrEqual<T>(T a, T b) {
         Compare(a, b, r => r >= 0, ">");
      }

      public static void Less<T>(T a, T b) {
         Compare(a, b, r => r < 0, ">");
      }

      public static void LessOrEqual<T>(T a, T b) {
         Compare(a, b, r => r <= 0, ">");
      }

      public static void IsInstanceOf(Type t, object o) {
         IsNotNull(o);
         Throw(o.GetType() == t, () => string.Format("Object type mismatch, expected {0} found {1}", t.FullName, o.GetType().FullName));
      }

      public static void IsNotInstanceOf(Type t, object o) {
         IsNotNull(o);
         Throw(o.GetType() != t, () => string.Format("Object type mismatch, should not be {0}", t.FullName));
      }

      public static void IsInstanceOf<T>(object o) {
         IsInstanceOf(typeof(T), o);
      }

      public static void IsNotInstanceOf<T>(object o) {
         IsNotInstanceOf(typeof(T), o);
      }

      public static void IsAssignableFrom(Type t, object o) {
         IsNotNull(o);
         Throw(o.GetType().IsAssignableFrom(t), () => string.Format("Object type mismatch, expected a type assignable from {0} found {1}", t.FullName, o.GetType().FullName));
      }

      public static void IsNotAssignableFrom(Type t, object o) {
         IsNotNull(o);
         Throw(!o.GetType().IsAssignableFrom(t), () => string.Format("Object type mismatch, didn't expect a type assignable from {0} found {1}", t.FullName, o.GetType().FullName));
      }

      public static void IsAssignableFrom<T>(object o) {
         IsAssignableFrom(typeof(T), o);
      }

      public static void IsNotAssignableFrom<T>(object o) {
         IsNotAssignableFrom(typeof(T), o);
      }

      public static void IsAssignableTo(Type t, object o) {
         IsNotNull(o);
         Throw(t.IsAssignableFrom(o.GetType()), () => string.Format("Object type mismatch, expected a type assignable to {0} found {1}", t.FullName, o.GetType().FullName));
      }

      public static void IsNotAssignableTo(Type t, object o) {
         IsNotNull(o);
         Throw(!t.IsAssignableFrom(o.GetType()), () => string.Format("Object type mismatch, didn't expect a type assignable to {0} found {1}", t.FullName, o.GetType().FullName));
      }

      public static void IsAssignableTo<T>(object o) {
         IsAssignableTo(typeof(T), o);
      }

      public static void IsNotAssignableTo<T>(object o) {
         IsNotAssignableTo(typeof(T), o);
      }

      public static Exception Throws(Type t, Action code) {
         try {
            code();
         } catch (Exception x) {
            Throw(t.IsAssignableFrom(x.GetType()), () => string.Format("Wrong exception type caught, expected {0} received {1}", t.FullName, Utils.FormatValue(x)));
            return x;
         }
         throw new AssertionException(string.Format("Failed to throw exception of type {0}", t.FullName));
      }

      public static TX Throws<TX>(Action code) where TX : Exception {
         return (TX)Throws(typeof(TX), code);
      }

      public static void DoesNotThrow(Action code) {
         try {
            code();
         } catch (Exception x) {
            Throw(false, () => string.Format("Unexpected exception {0}", Utils.FormatValue(x)));
         }
      }

      public static void ForEach<TSource>(IEnumerable<TSource> source, Action<TSource> code) {
         int index = 0;
         foreach (var i in source) {
            try {
               code(i);
            } catch (Exception x) {
               throw new AssertionException(string.Format("Collection assertion failed at item {0}\n  Collection: {1}\n  Inner Exception: {2}", index, Utils.FormatValue(source), x.Message));
            }
            index++;
         }
      }

      public static void AllItemsAreNotNull<T>(IEnumerable<T> coll) {
         int index = 0;
         foreach (var i in coll) {
            if (i == null) {
               throw new AssertionException(string.Format("Collection has a null item at index {0}", index));
            }
            index++;
         }
      }

      public static void AllItemsAreUnique<T>(IEnumerable<T> coll) {
         var list = coll.ToList();
         for (int i = 0; i < list.Count; i++) {
            for (int j = i + 1; j < list.Count; j++) {
               if (object.Equals(list[i], list[j]))
                  throw new AssertionException(string.Format("Collection items are not unique\n  [{0}] = {1}\n  [{2}] = {3}\n", i, list[i], j, list[j]));
            }
         }
      }

      public static void AllItemsAreEqual<Ta, Tb>(IEnumerable<Ta> a, IEnumerable<Tb> b, Func<Ta, Tb, bool> CompareEqual) {
         var e1 = a.GetEnumerator();
         var e2 = b.GetEnumerator();
         int index = 0;
         while (true) {
            bool have1 = e1.MoveNext();
            bool have2 = e2.MoveNext();
            if (!have1 && !have2)
               return;
            if (!have1 || !have2 || !CompareEqual(e1.Current, e2.Current))
               throw new AssertionException(string.Format("Collection are not equal at index {0}\n  a[{0}] = {1}\n  b[{0}] = {2}\n", index, e1.Current, e2.Current));
            index++;
         }
      }

      public static void AllItemsAreEqual<T>(IEnumerable<T> a, IEnumerable<T> b) {
         AllItemsAreEqual<T, T>(a, b, (x, y) => object.Equals(x, y));
      }

      public static void AllItemsAreInstancesOf(Type t, System.Collections.IEnumerable coll) {
         int index = 0;
         foreach (object o in coll) {
            if (o == null || o.GetType() != t) {
               throw new AssertionException(string.Format("Collection item at index {0} is of the wrong type, expected {1} but found {2}", index, t.FullName, o == null ? "null" : o.GetType().FullName));
            }
            index++;
         }
      }

      public static void AllItemsAreInstancesOf<T>(System.Collections.IEnumerable coll) {
         AllItemsAreInstancesOf(typeof(T), coll);
      }

      private static int IndexOf<Ta, Tb>(Ta Item, List<Tb> list, Func<Ta, Tb, bool> CompareEqual) {
         for (int j = 0; j < list.Count; j++) {
            if (CompareEqual(Item, list[j]))
               return j;
         }
         return -1;
      }

      public static void IsSubsetOf<Ta, Tb>(IEnumerable<Ta> subset, IEnumerable<Tb> superset, Func<Ta, Tb, bool> CompareEqual) {
         var list = superset.ToList();
         int index = 0;
         foreach (var i in subset) {
            int pos = IndexOf<Ta, Tb>(i, list, CompareEqual);
            if (pos < 0)
               throw new AssertionException(string.Format("Collection is not a subset (check subset index {0}\n  subset =   {1}\n  superset = {2}", index, Utils.FormatValue(subset), Utils.FormatValue(superset)));
            list.RemoveAt(pos);
            index++;
         }
      }

      public static void IsSubsetOf<T>(IEnumerable<T> subset, IEnumerable<T> superset) {
         IsSubsetOf<T, T>(subset, superset, (x, y) => Object.Equals(x, y));
      }

      public static void IsNotSubsetOf<Ta, Tb>(IEnumerable<Ta> subset, IEnumerable<Tb> superset, Func<Ta, Tb, bool> CompareEqual) {
         var list = superset.ToList();
         foreach (var i in subset) {
            int pos = IndexOf<Ta, Tb>(i, list, CompareEqual);
            if (pos < 0)
               return;
            list.RemoveAt(pos);
         }
         throw new AssertionException(string.Format("Collection is a subset\n  subset =   {0}\n  superset = {1}", Utils.FormatValue(subset), Utils.FormatValue(superset)));
      }

      public static void IsNotSubsetOf<T>(IEnumerable<T> subset, IEnumerable<T> superset) {
         IsNotSubsetOf<T, T>(subset, superset, (x, y) => Object.Equals(x, y));
      }


      static bool TestEquivalent<Ta, Tb>(IEnumerable<Ta> a, IEnumerable<Tb> b, Func<Ta, Tb, bool> CompareEqual) {
         var list = b.ToList();
         foreach (var i in a) {
            int pos = IndexOf(i, list, CompareEqual);
            if (pos < 0)
               return false;
            list.RemoveAt(pos);
         }
         return list.Count == 0;
      }

      public static void AreEquivalent<Ta, Tb>(IEnumerable<Ta> a, IEnumerable<Tb> b, Func<Ta, Tb, bool> CompareEqual) {
         Throw(TestEquivalent(a, b, CompareEqual), () => string.Format("Collections are not equivalent\n  lhs: {0}\n  rhs: {1}", Utils.FormatValue(a), Utils.FormatValue(b)));
      }

      public static void AreNotEquivalent<Ta, Tb>(IEnumerable<Ta> a, IEnumerable<Tb> b, Func<Ta, Tb, bool> CompareEqual) {
         Throw(!TestEquivalent(a, b, CompareEqual), () => string.Format("Collections are not equivalent\n  lhs: {0}\n  rhs: {1}", Utils.FormatValue(a), Utils.FormatValue(b)));
      }

      public static void AreEquivalent<T>(IEnumerable<T> a, IEnumerable<T> b) {
         AreEquivalent<T, T>(a, b, (x, y) => Object.Equals(x, y));
      }
      public static void AreNotEquivalent<T>(IEnumerable<T> a, IEnumerable<T> b) {
         AreNotEquivalent<T, T>(a, b, (x, y) => Object.Equals(x, y));
      }
   }
}
