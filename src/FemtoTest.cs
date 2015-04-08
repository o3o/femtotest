using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Linq.Expressions;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FemtoTest {
   // Use to mark a class as a testfixture
   [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
   public class TestFixtureAttribute: TestBaseAttribute {
      public TestFixtureAttribute(params object[] args) : base(args) { }
   }

   // Use to mark a method as a test
   [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
   public class TestAttribute: TestBaseAttribute {
      public TestAttribute(params object[] args) : base(args) { }
   }

   // Base class for Test and TestFixture attributes
   public abstract class TestBaseAttribute: Attribute {
      public TestBaseAttribute(params object[] Arguments) {
         this.Arguments = Arguments;
      }

      public object[] Arguments {
         get;
         private set;
      }

      public string Source {
         get;
         set;
      }

      public bool Active {
         get;
         set;
      }

      public virtual IEnumerable<object[]> GetArguments(Type owningType, object testFixtureInstance) {
         if (Source != null) {
            if (testFixtureInstance != null) {
               var iter_method = owningType.GetMethod(Source, BindingFlags.Instance | BindingFlags.Public);
               return (IEnumerable<object[]>)iter_method.Invoke(testFixtureInstance, null);
            } else {
               var iter_method = owningType.GetMethod(Source, BindingFlags.Static | BindingFlags.Public);
               return (IEnumerable<object[]>)iter_method.Invoke(null, null);
            }
         } else {
            return new object[][] { Arguments };
         }
      }
   }

   [AttributeUsage(AttributeTargets.Method)]
   public class SetUpAttribute: SetUpTearDownAttributeBase {
      public SetUpAttribute(): base(true, false) { }
   }

   [AttributeUsage(AttributeTargets.Method)]
   public class TearDownAttribute: SetUpTearDownAttributeBase {
      public TearDownAttribute(): base(false, false) { }
   }

   [AttributeUsage(AttributeTargets.Method)]
   public class TestFixtureSetUpAttribute : SetUpTearDownAttributeBase {
      public TestFixtureSetUpAttribute() : base(true, true) { }
   }

   [AttributeUsage(AttributeTargets.Method)]
   public class TestFixtureTearDownAttribute: SetUpTearDownAttributeBase {
      public TestFixtureTearDownAttribute(): base(false, true) { }
   }

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

   // Runner - runs a set of tests
   public class Runner {
      public ResultsWriter Output {
         get;
         set;
      }

      public static bool? BoolFromString(string str) {
         if (string.IsNullOrWhiteSpace(str)) return null;
         str = str.ToLowerInvariant();
         return (str == "yes" || str == "y" || str == "true" || str == "t" || str == "1");
      }

      public static int RunMain(string[] args) {
         return new Runner().Run(args);
      }

      // Run all test fixtures in the calling assembly - unless one or more
      // marked as active in which case only those will be run
      public int Run(string[] args) {
         // Parse command line args
         bool? runall = null;
         bool verbose = false;
         bool debug = false;
         string output_file = null;
         foreach (string a in args) {
            switch (a) {
               case "-d":
                  debug = true;
                  break;
               case "-h":
                  Console.WriteLine("-d show Console.WriteLine output in the console while the test runs");
                  Console.WriteLine("-v show test name when test runs");
                  Console.WriteLine("-a run all tests even if one or more are marked as active");
                  Console.WriteLine("-h show help");
                  break;
               case "-a":
                  runall = true;
                  break;
               case "-v":
                  verbose = true;
                  break;
               default:
                  Console.WriteLine("Warning: Unknown switch '{0}' ignored", a);
                  break;
            }
         }

         Output = new PlainTextResultsWriter(null);
         Output.Verbose = verbose;
         Output.Debug = debug;

         var totalTime = Stopwatch.StartNew();

         _statsStack.Clear();
         _statsStack.Push(new Stats());
         var old = Console.Out;
         if (!Output.Debug) {
            Console.SetOut(Output);
         }
         RunInternal(Assembly.GetCallingAssembly(), null, null, runall ?? false);

         if (!Output.Debug) {
            Console.SetOut(old);
         }
         totalTime.Stop();

         Output.Complete(_statsStack.Pop(), _otherTimes.ElapsedMilliseconds, totalTime.ElapsedMilliseconds);

         return 0;
      }

      // Helper to create instances of test fixtures
      private object CreateInstance(Type t, object[] args) {
         try {
            _otherTimes.Start();
            return Activator.CreateInstance(t, args);
         } catch (Exception x) {
            Output.WriteException(x);
            Stats.Errors++;
            return null;
         } finally {
            _otherTimes.Stop();
         }
      }

      // Internally called to recursively run tests in an assembly, testfixture, test method etc...
      private void RunInternal(object scope, object instance, object[] arguments, bool RunAll) {
         // Assembly?
         var a = scope as Assembly;
         if (a != null) {
            StartTest(a, null);
            RunAll = RunAll || !a.HasActive();
            foreach (var type in a.GetTypes().Where(i => i.IsTestFixture() && (RunAll || i.HasActive())))
               RunInternal(type, null, null, RunAll);
            EndTest();
         }

         // Test Fixture class
         var t = scope as Type;
         if (t != null) {
            if (arguments == null) {
               bool runAllTestFixturesInstances = RunAll || !t.IsActive();
               bool runAllTestMethods = RunAll || !t.HasActiveMethods();
               foreach (TestFixtureAttribute tfa in t.GetCustomAttributes(typeof(TestFixtureAttribute), false).Where(x => runAllTestFixturesInstances || ((TestFixtureAttribute)x).Active))
                  foreach (var args in tfa.GetArguments(t, null)) {
                     RunInternal(t, null, args, runAllTestMethods);
                  }
            } else {
               StartTest(t, arguments);
               var inst = CreateInstance(t, arguments);
               if (inst != null) {
                  RunInternal(null, inst, null, RunAll);
               }
               EndTest();
            }
         }

         // Test Fixture instance
         if (instance != null && instance.GetType().IsTestFixture()) {
            var tf = instance;
            if (scope == null) {
               if (!RunSetupTeardown(instance, true, true))
                  return;

               foreach (var m in tf.GetType().GetMethods().Where(x => RunAll || x.IsActive()))
                  RunInternal(m, instance, null, RunAll);

               RunSetupTeardown(instance, false, true);
            }

            var method = scope as MethodInfo;
            if (method != null) {
               if (arguments == null) {
                  foreach (TestAttribute i in method.GetCustomAttributes(typeof(TestAttribute), false).Where(x => RunAll || ((TestAttribute)x).Active))
                     foreach (var args in i.GetArguments(method.DeclaringType, instance)) {
                        if (args.Length != method.GetParameters().Length) {
                           Output.WriteWarning("{0} provided in an incorrect number of arguments (expected {1} but found {2}) - skipped", i.GetType().FullName, method.GetParameters().Length, args.Length);
                           Stats.Warnings++;
                           continue;
                        }

                        RunInternal(method, instance, args, RunAll);
                     }
               } else {
                  RunTest(method, tf, arguments);
               }
            }
         }
      }

      // Run a single test
      public void RunTest(MethodInfo Target, object instance, object[] Params) {
         StartTest(Target, Params);
         var sw = new Stopwatch();
         try {
            if (!RunSetupTeardown(instance, true, false)) {
               EndTest();
               return;
            }
            sw.Start();
            Target.Invoke(instance, Params);
            Stats.Elapsed = sw.ElapsedMilliseconds;
            Stats.Passed++;
         } catch (Exception x) {
            Stats.Elapsed = sw.ElapsedMilliseconds;
            var invoc = x as TargetInvocationException;
            if (invoc != null)
               x = invoc.InnerException;
            Output.WriteException(x);
            Stats.Errors++;
         }
         RunSetupTeardown(instance, false, false);
         EndTest();
      }

      Stopwatch _otherTimes = new Stopwatch();

      [SkipInStackTrace]
         public bool RunSetupTeardown(object instance, bool setup, bool fixture) {
            try {
               foreach (var m in instance.GetType().GetMethods().Where(x => x.GetCustomAttributes(typeof(SetUpTearDownAttributeBase), false)
                        .Cast<SetUpTearDownAttributeBase>().Any((SetUpTearDownAttributeBase y) => y.ForSetup == setup && y.ForFixture == fixture))) {
                  _otherTimes.Start();
                  try {
                     m.Invoke(instance, null);
                  } finally {
                     _otherTimes.Stop();
                  }
               }
               return true;
            } catch (Exception x) {
               var invoc = x as TargetInvocationException;
               if (invoc != null)
                  x = invoc.InnerException;
               Output.WriteException(x);
               Stats.Errors++;
               return false;
            }
         }


      private void StartTest(object Target, object[] Params) {
         var stats = new Stats() {
            Target = Target
         };
         _statsStack.Push(stats);
         Output.StartTest(Target, Params);
      }

      private void EndTest() {
         var old = Stats;
         _statsStack.Pop();
         Stats.Add(old);
         Output.EndTest(old);
      }

      private Stack<Stats> _statsStack = new Stack<Stats>();
      public Stats Stats {
         get {
            return _statsStack.Peek();
         }
      }
   }

   public class Stats {
      public object Target {
         get;
         set;
      }
      public int Errors {
         get;
         set;
      }
      public int Warnings {
         get;
         set;
      }
      public int Passed {
         get;
         set;
      }
      public long Elapsed {
         get;
         set;
      }

      public void Add(Stats other) {
         Errors += other.Errors;
         Warnings += other.Warnings;
         Passed += other.Passed;
         Elapsed += other.Elapsed;
      }
   }

   // The exception thrown when an assertion fails
   public class AssertionException : Exception {
      public AssertionException(string message) : base(message) { }
   }

   // Used to mark utility functions that throw assertion exceptions so the stack trace can be unwound to the actual place the assertion originates
   [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
      public class SkipInStackTraceAttribute : Attribute {
      }

   // Base class for setup/teardown attributes
   public class SetUpTearDownAttributeBase : Attribute {
      public SetUpTearDownAttributeBase(bool forSetup, bool forFixture) {
         this.ForSetup = forSetup;
         this.ForFixture = forFixture;
      }
      public bool ForSetup {
         get;
         set;
      }
      public bool ForFixture {
         get;
         set;
      }
   }

   // A bunch of utility functions and extension methods
   public static class Utils {
      public static IEnumerable<StackFrame> SimplifyStackTrace(StackTrace st) {
         foreach (var f in st.GetFrames()) {
            if (f.GetMethod().GetCustomAttributes(typeof(SkipInStackTraceAttribute), false).Length != 0 ||
                  (f.GetMethod().DeclaringType != null && f.GetMethod().DeclaringType.GetCustomAttributes(typeof(SkipInStackTraceAttribute), false).Length != 0) ||
                  f.GetFileName() == null)
               continue;

            if (f.GetMethod().IsSpecialName | f.GetMethod().Name.StartsWith("<"))
               break;

            yield return f;
         }
      }

      public static IEnumerable<Tuple<int, string>> ExtractLinesFromTextFile(string file, int line, int extra = 2) {
         try {
            if (line <= extra)
               line = extra + 1;

            return System.IO.File.ReadAllLines(file).Skip(line - extra - 1).Take(extra * 2 + 1).Select((l, i) => new Tuple<int, string>(i + line - extra, l));
         } catch (Exception) {
            return new Tuple<int, string>[] { };
         }
      }

      // Format any value for diagnostic display
      public static string FormatValue(object value) {
         if (value == null)
            return "null";

         var str = value as string;
         if (str != null) {
            str = str.Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\0", "\\0");
            return string.Format("\"{0}\"", str);
         }

         if (value.GetType() == typeof(int) || value.GetType() == typeof(long) || value.GetType() == typeof(bool))
            return value.ToString();

         var d = value as System.Collections.IDictionary;
         if (d != null)
            return string.Format("{{{0}}}", string.Join(", ", d.AsDictionaryEntries().Select(de => string.Format("{{ {0}, {1} }}", FormatValue(de.Key), FormatValue(de.Value)))));

         var e = value as System.Collections.IEnumerable;
         if (e != null)
            return string.Format("[{0}]", string.Join(", ", e.Cast<object>().Select(v => FormatValue(v))));

         var x = value as Exception;
         if (x != null)
            return string.Format("[{0}] {1}", value.GetType().FullName, x.Message);

         return string.Format("[{0}] {1}", value.GetType().FullName, value.ToString());
      }

      public static IEnumerable<System.Collections.DictionaryEntry> AsDictionaryEntries(this System.Collections.IDictionary dictionary) {
         foreach (var de in dictionary)
            yield return (System.Collections.DictionaryEntry)de;
      }

      public static string FormatArguments(object[] args) {
         return string.Format("({0})", args == null ? "" : string.Join(", ", args.Select(v => FormatValue(v))));
      }

      // Format the name of a test target
      public static string FormatTarget(object o) {
         var mb = o as MethodBase;
         if (mb != null)
            return "test " + mb.Name;
         var t = o as Type;
         if (t != null && t.IsClass)
            return "testfixture " + t.Name;
         var a = o as Assembly;
         if (a != null)
            return "assembly " + a.FullName;
         return null;
      }

      public static int CountCommonPrefix(string a, string b, bool IgnoreCase) {
         int i = 0;
         while (i < Math.Min(a.Length, b.Length) && (IgnoreCase ? (char.ToUpperInvariant(a[i]) == char.ToUpperInvariant(b[i])) : (a[i] == b[i])))
            i++;
         return i;
      }

      public static string GetStringExtract(string str, int offset) {
         if (offset > 15)
            str = "..." + str.Substring(offset - 10);
         if (str.Length > 30)
            str = str.Substring(0, 20) + "...";
         return str;
      }

      public static bool IsTestFixture(this Type t) {
         return t.IsClass && !t.IsAbstract && t.GetCustomAttributes(typeof(TestFixtureAttribute), false).Any();
      }

      public static bool IsTestMethod(this MethodInfo mi) {
         return mi.GetCustomAttributes(typeof(TestAttribute), false).Any();
      }

      public static bool IsActive(this ICustomAttributeProvider p) {
         return p.GetCustomAttributes(typeof(TestBaseAttribute), false).Any(a => ((TestBaseAttribute)a).Active);
      }

      public static bool HasActiveMethods(this Type t) {
         return t.GetMethods().Any(m => m.IsActive());
      }

      public static bool HasActive(this Type t) {
         return t.IsActive() || t.HasActiveMethods();
      }

      public static bool HasActive(this Assembly a) {
         return a.GetTypes().Any(t => t.HasActive());
      }
   }

   // Base class for result writers
   public abstract class ResultsWriter: TextWriter {
      public bool Verbose;
      public bool Debug;
      public abstract void StartTest(object Target, object[] Arguments);
      public abstract void EndTest(Stats stats);
      public virtual void Complete(Stats stats, long OtherTimes, long ActualTime) { }

      public virtual void WriteWarning(string str, params object[] args) {
         WriteLine(str, args);
      }

      public virtual void WriteError(string str, params object[] args) {
         WriteLine(str, args);
      }

      public abstract void WriteException(Exception x);
   }

   // Plain text results writer (aka console output)
   public class PlainTextResultsWriter: ResultsWriter {
      TextWriter target;
      public PlainTextResultsWriter(TextWriter target = null) {
         this.target = target == null ? Console.Out : target;
      }

      public override void StartTest(object Target, object[] Arguments) {
         if (Verbose) {
            WriteIndented(string.Format("{0}{1}\n", Utils.FormatTarget(Target), Utils.FormatArguments(Arguments)));
            _indentDepth += (Target as MethodBase) != null ? 2 : 1;
         }
      }

      public override void EndTest(Stats stats) {
         _indentDepth -= (stats.Target as MethodBase) != null ? 2 : 1;
      }

      public override void Complete(Stats stats, long OtherTimes, long ActualTime) {
         bool Success = stats.Errors == 0 && stats.Warnings == 0;
         var delim = new string(Success ? '-' : '*', 40);
         target.WriteLine("\nTest cases:     {0,10:#,##0}ms\nSetup/teardown: {1,10:#,##0}ms\nTest framework: {2,10:#,##0}ms",
               stats.Elapsed, OtherTimes, ActualTime - (stats.Elapsed + OtherTimes));
         if (Success)
            target.WriteLine("\n{0}\nAll {1} tests passed\n{0}\n", delim, stats.Passed, stats.Elapsed);
         else
            target.WriteLine("\n{0}\n{1} Errors, {2} Warnings, {3} passed\n{0}\n", delim, stats.Errors, stats.Warnings, stats.Passed);

         target.Flush();
      }

      public override void Write(char value) {
         Write(value.ToString());
      }

      public override void Write(char[] buffer, int index, int count) {
         Write(new String(buffer, index, count));
      }

      public override void Write(string str) {
         if (Verbose)
            WriteIndented(str);
      }

      public void WriteIndented(string str) {
         string indent = new string(' ', _indentDepth * 2);
         if (_indentPending)
            target.Write(indent);

         _indentPending = str.EndsWith("\n");
         if (_indentPending)
            str = str.Substring(0, str.Length - 1);

         str = str.Replace("\n", "\n" + indent);

         target.Write(str);

         if (_indentPending)
            target.Write("\n");
      }

      public override Encoding Encoding {
         get { return Encoding.UTF8; }
      }

      public override void WriteException(Exception x) {
         var assert = x as AssertionException;
         if (assert != null)
            WriteIndented(string.Format("\nAssertion failed - {0}\n\n", assert.Message));
         else
            WriteIndented(string.Format("\nException {0}: {1}\n\n", x.GetType().FullName, x.Message));

         StackFrame first = null;
         foreach (var f in Utils.SimplifyStackTrace(new StackTrace(x, true))) {
            if (first == null) first = f;
            WriteIndented(string.Format("  {0} - {1}({2})\n", f.GetMethod().Name, f.GetFileName(), f.GetFileLineNumber()));
         }

         if (first != null) {
            WriteIndented("\n");
            foreach (var l in Utils.ExtractLinesFromTextFile(first.GetFileName(), first.GetFileLineNumber()))
               WriteIndented(string.Format("  {0:00000}:{1}{2}\n", l.Item1, l.Item1 == first.GetFileLineNumber() ? "->" : "  ", l.Item2));
         }

         WriteIndented("\n");
      }

      bool _indentPending = true;
      int _indentDepth = 0;
   }
}
