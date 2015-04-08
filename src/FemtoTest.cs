using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Diagnostics;

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

   // Runner - runs a set of tests
   public class Runner {
      private ResultsWriter writer;

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
         writer = new PlainTextResultsWriter(null);
         foreach (string a in args) {
            switch (a) {
               case "-d":
                  writer.Debug = true;
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
                  writer.Verbose = true;
                  break;
               default:
                  Console.WriteLine("Warning: Unknown switch '{0}' ignored", a);
                  break;
            }
         }
         var totalTime = Stopwatch.StartNew();

         _statsStack.Clear();
         _statsStack.Push(new Stats());
         var old = Console.Out;
         if (!writer.Debug) {
            Console.SetOut(writer);
         }
         RunInternal(Assembly.GetCallingAssembly(), null, null, runall ?? false);

         if (!writer.Debug) {
            Console.SetOut(old);
         }
         totalTime.Stop();

         writer.Complete(_statsStack.Pop(), _otherTimes.ElapsedMilliseconds, totalTime.ElapsedMilliseconds);
         return 0;
      }

      // Helper to create instances of test fixtures
      private object CreateInstance(Type t, object[] args) {
         try {
            _otherTimes.Start();
            return Activator.CreateInstance(t, args);
         } catch (Exception x) {
            writer.WriteException(x);
            Stats.Errors++;
            return null;
         } finally {
            _otherTimes.Stop();
         }
      }

      // Internally called to recursively run tests in an assembly, testfixture, test method etc...
      private void RunInternal(object scope, object instance, object[] arguments, bool runAll) {
         // Assembly?
         var a = scope as Assembly;
         if (a != null) {
            StartTest(a, null);
            runAll = runAll || !a.HasActive();
            foreach (var type in a.GetTypes().Where(i => i.IsTestFixture() && (runAll || i.HasActive()))) {
               RunInternal(type, null, null, runAll);
            }
            EndTest();
         }

         // Test Fixture class
         var t = scope as Type;
         if (t != null) {
            if (arguments == null) {
               bool runAllTestFixturesInstances = runAll || !t.IsActive();
               bool runAllTestMethods = runAll || !t.HasActiveMethods();
               foreach (TestFixtureAttribute tfa in t.GetCustomAttributes(typeof(TestFixtureAttribute), false).Where(x => runAllTestFixturesInstances || ((TestFixtureAttribute)x).Active))
                  foreach (var args in tfa.GetArguments(t, null)) {
                     RunInternal(t, null, args, runAllTestMethods);
                  }
            } else {
               StartTest(t, arguments);
               var inst = CreateInstance(t, arguments);
               if (inst != null) {
                  RunInternal(null, inst, null, runAll);
               }
               EndTest();
            }
         }

         // Test Fixture instance
         if (instance != null && instance.GetType().IsTestFixture()) {
            var tf = instance;
            if (scope == null) {
               if (!RunSetupTeardown(instance, true, true)) {
                  return;
               }

               foreach (var m in tf.GetType().GetMethods().Where(x => runAll || x.IsActive())) {
                  RunInternal(m, instance, null, runAll);
               }

               RunSetupTeardown(instance, false, true);
            }

            var method = scope as MethodInfo;
            if (method != null) {
               if (arguments == null) {
                  foreach (TestAttribute i in method.GetCustomAttributes(typeof(TestAttribute), false).Where(x => runAll || ((TestAttribute)x).Active))
                     foreach (var args in i.GetArguments(method.DeclaringType, instance)) {
                        if (args.Length != method.GetParameters().Length) {
                           writer.WriteWarning("{0} provided in an incorrect number of arguments (expected {1} but found {2}) - skipped", i.GetType().FullName, method.GetParameters().Length, args.Length);
                           Stats.Warnings++;
                           continue;
                        }

                        RunInternal(method, instance, args, runAll);
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
            writer.WriteException(x);
            Stats.Errors++;
         }
         RunSetupTeardown(instance, false, false);
         EndTest();
      }

      Stopwatch _otherTimes = new Stopwatch();

      [SkipInStackTrace] public bool RunSetupTeardown(object instance, bool setup, bool fixture) {
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
            writer.WriteException(x);
            Stats.Errors++;
            return false;
         }
      }

      private void StartTest(object Target, object[] Params) {
         var stats = new Stats() {
            Target = Target
         };
         _statsStack.Push(stats);
         writer.StartTest(Target, Params);
      }

      private void EndTest() {
         var old = Stats;
         _statsStack.Pop();
         Stats.Add(old);
         writer.EndTest(old);
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
      public class SkipInStackTraceAttribute : Attribute { }

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
         if (x != null) {
            return string.Format("[{0}] {1}", value.GetType().FullName, x.Message);
         }

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

      public static int CountCommonPrefix(string a, string b, bool ignoreCase) {
         int i = 0;
         while (i < Math.Min(a.Length, b.Length) && (ignoreCase ? (char.ToUpperInvariant(a[i]) == char.ToUpperInvariant(b[i])) : (a[i] == b[i]))) {
            i++;
         }
         return i;
      }

      public static string GetStringExtract(string str, int offset) {
         if (offset > 15) {
            str = "..." + str.Substring(offset - 10);
         }
         if (str.Length > 30) {
            str = str.Substring(0, 20) + "...";
         }
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
      private bool _indentPending = true;
      private int _indentDepth = 0;
      private readonly TextWriter target;

      public PlainTextResultsWriter(TextWriter target = null) {
         this.target = target == null ? Console.Out : target;
      }

      public override void StartTest(object target, object[] arguments) {
         if (Verbose) {
            WriteIndented(string.Format("{0}{1}\n", Utils.FormatTarget(target), Utils.FormatArguments(arguments)));
            _indentDepth += (target as MethodBase) != null ? 2 : 1;
         }
      }

      public override void EndTest(Stats stats) {
         _indentDepth -= (stats.Target as MethodBase) != null ? 2 : 1;
      }

      public override void Complete(Stats stats, long otherTimes, long actualTime) {
         bool success = stats.Errors == 0 && stats.Warnings == 0;
         var delim = new string(success ? '-' : '*', 40);
         target.WriteLine("\nTest cases:     {0,10:#,##0}ms\nSetup/teardown: {1,10:#,##0}ms\nTest framework: {2,10:#,##0}ms",
               stats.Elapsed, otherTimes, actualTime - (stats.Elapsed + otherTimes));
         if (success) {
            target.WriteLine("\n{0}\nAll {1} tests passed\n{0}\n", delim, stats.Passed, stats.Elapsed);
         } else {
            target.WriteLine("\n{0}\n{1} Errors, {2} Warnings, {3} passed\n{0}\n", delim, stats.Errors, stats.Warnings, stats.Passed);
         }

         target.Flush();
      }

      public override void Write(char value) {
         Write(value.ToString());
      }

      public override void Write(char[] buffer, int index, int count) {
         Write(new String(buffer, index, count));
      }

      public override void Write(string str) {
         if (Verbose) {
            WriteIndented(str);
         }
      }

      public void WriteIndented(string str) {
         string indent = _indentDepth > 0
            ? new string(' ', _indentDepth * 2 )
            : string.Empty;

         if (_indentPending) {
            target.Write(indent);
         }

         _indentPending = str.EndsWith("\n");
         if (_indentPending) {
            str = str.Substring(0, str.Length - 1);
         }

         str = str.Replace("\n", "\n" + indent);

         target.Write(str);

         if (_indentPending) {
            target.Write("\n");
         }
      }

      public override Encoding Encoding {
         get { return Encoding.UTF8; }
      }

      public override void WriteException(Exception x) {
         var assert = x as AssertionException;
         if (assert != null) {
            WriteIndented(string.Format("\nAssertion failed - {0} {1}\n\n", assert.Message, assert.Source));
         } else {
            WriteIndented(string.Format("\nException {0}: {1}\n\n", x.GetType().FullName, x.Message));
         }

         StackFrame first = null;
         var stackTrace = new StackTrace(x, true);
         foreach (var f in Utils.SimplifyStackTrace(stackTrace)) {
         //foreach (var f in stackTrace.GetFrames()) {
            if (first == null) first = f;
            WriteIndented(string.Format("  {0} - [{1}]({2})\n", f.GetMethod().Name, f.GetFileName(), f.GetFileLineNumber()));
         }

         if (first != null) {
            WriteIndented("\n");
            foreach (var l in Utils.ExtractLinesFromTextFile(first.GetFileName(), first.GetFileLineNumber())) {
               WriteIndented(string.Format("  {0:00000}:{1}{2}\n", l.Item1, l.Item1 == first.GetFileLineNumber() ? "->" : "  ", l.Item2));
            }
         }

         WriteIndented("\n");
      }
   }
}
