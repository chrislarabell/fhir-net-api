﻿/* 
 * Copyright (c) 2015, Firely (info@fire.ly) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/ewoutkramer/fhir-net-api/master/LICENSE
 */

using System;
using System.Linq;
using System.Collections.Generic;
using Hl7.FhirPath.Functions;
using System.Text.RegularExpressions;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model.Primitives;

namespace Hl7.FhirPath.Expressions
{
    public static class SymbolTableInit
    {
        public static SymbolTable AddStandardFP(this SymbolTable t)
        {
            // Functions that operate on the focus, without null propagation
            t.Add("empty", (IEnumerable<object> f) => !f.Any());
            t.Add("exists", (IEnumerable<object> f) => f.Any());
            t.Add("count", (IEnumerable<object> f) => f.Count());

            t.Add("allTrue", (IEnumerable<ITypedElement> f) => f.All(e => e.Value as bool? == true));

            t.Add("trace", (IEnumerable<ITypedElement> f, string name, EvaluationContext ctx) 
                    => f.Trace(name, ctx));

            t.Add("combine", (IEnumerable<ITypedElement> l, IEnumerable<ITypedElement> r) => l.Concat(r));
            t.Add("binary.|", (object f, IEnumerable<ITypedElement> l, IEnumerable<ITypedElement> r) => l.DistinctUnion(r));
            t.Add("union", (IEnumerable<ITypedElement> l, IEnumerable<ITypedElement> r) => l.DistinctUnion(r));
            t.Add("binary.contains", (object f, IEnumerable<ITypedElement> a, ITypedElement b) => a.Contains(b));
            t.Add("binary.in", (object f, ITypedElement a, IEnumerable<ITypedElement> b) => b.Contains(a));
            t.Add("distinct", (IEnumerable<ITypedElement> f) => f.Distinct());
            t.Add("isDistinct", (IEnumerable<ITypedElement> f) => f.IsDistinct());
            t.Add("subsetOf", (IEnumerable<ITypedElement> f, IEnumerable<ITypedElement> a) => f.SubsetOf(a));
            t.Add("supersetOf", (IEnumerable<ITypedElement> f, IEnumerable<ITypedElement> a) => a.SubsetOf(f));
            t.Add("intersect", (IEnumerable<ITypedElement> f, IEnumerable<ITypedElement> a) => f.Intersect(a));
            t.Add("exclude", (IEnumerable<ITypedElement> f, IEnumerable<ITypedElement> a) => f.Exclude(a));

            t.Add("today", (object f) => PartialDateTime.Today());
            t.Add("now", (object f) => PartialDateTime.Now());

            t.Add("binary.&", (object f, string a, string b) => (a ?? "") + (b ?? ""));

            t.Add("iif", (IEnumerable<ITypedElement> f, bool? condition, IEnumerable<ITypedElement> result) => f.IIf(condition, result));
            t.Add("iif", (IEnumerable<ITypedElement> f, bool? condition, IEnumerable<ITypedElement> result, IEnumerable<ITypedElement> otherwise) => f.IIf(condition, result, otherwise));

            // Functions that use normal null propagation and work with the focus (buy may ignore it)
            t.Add("not", (IEnumerable<ITypedElement> f) => f.Not(), doNullProp:true);
            t.Add("builtin.children", (IEnumerable<ITypedElement> f, string a) => f.Navigate(a), doNullProp: true);

            t.Add("children", (IEnumerable<ITypedElement> f) => f.Children(), doNullProp: true);
            t.Add("descendants", (IEnumerable<ITypedElement> f) => f.Descendants(), doNullProp: true);

            t.Add("binary.=", (object f, IEnumerable<ITypedElement>  a, IEnumerable<ITypedElement> b) => a.IsEqualTo(b), doNullProp: true);
            t.Add("binary.!=", (object f, IEnumerable<ITypedElement> a, IEnumerable<ITypedElement> b) => !a.IsEqualTo(b), doNullProp: true);
            t.Add("binary.~", (object f, IEnumerable<ITypedElement> a, IEnumerable<ITypedElement> b) => a.IsEquivalentTo(b), doNullProp: true);
            t.Add("binary.!~", (object f, IEnumerable<ITypedElement> a, IEnumerable<ITypedElement> b) => !a.IsEquivalentTo(b), doNullProp: true);

            t.Add("unary.-", (object f, long a) => -a, doNullProp: true);
            t.Add("unary.-", (object f, decimal a) => -a, doNullProp: true);
            t.Add("unary.+", (object f, long a) => a, doNullProp: true);
            t.Add("unary.+", (object f, decimal a) => a, doNullProp: true);

            t.Add("binary.*", (object f, long a, long b) => a * b, doNullProp: true);
            t.Add("binary.*", (object f, decimal a, decimal b) => a * b, doNullProp: true);
            t.Add("binary.*", (object f, Quantity a, Quantity b) => a * b, doNullProp: true);

            t.Add("binary./", (object f, decimal a, decimal b) => a / b, doNullProp: true);
            t.Add("binary./", (object f, Quantity a, Quantity b) => a / b, doNullProp: true);
            //.Add((object f, decimal a, decimal b) => a / b, doNullProp: true);

            t.Add("binary.+", (object f, long a, long b) => a + b, doNullProp: true);
            t.Add("binary.+", (object f, decimal a, decimal b) => a + b, doNullProp: true);
            t.Add("binary.+", (object f, string a, string b) => a + b, doNullProp: true);
            t.Add("binary.+", (object f, Quantity a, Quantity b) => a + b, doNullProp: true);

            t.Add("binary.-", (object f, long a, long b) => a - b, doNullProp: true);
            t.Add("binary.-", (object f, decimal a, decimal b) => a - b, doNullProp: true);
            t.Add("binary.-", (object f, Quantity a, Quantity b) => a - b, doNullProp: true);

            t.Add("binary.div", (object f, long a, long b) => a / b, doNullProp: true);
            t.Add("binary.div", (object f, decimal a, decimal b) => (long)Math.Truncate(a / b), doNullProp: true);

            t.Add("binary.mod", (object f, long a, long b) => a % b, doNullProp: true);
            t.Add("binary.mod", (object f, decimal a, decimal b) => a % b, doNullProp: true);

            t.Add("binary.>", (object f, long a, long b) => a > b, doNullProp: true);
            t.Add("binary.>", (object f, decimal a, decimal b) => a > b, doNullProp: true);
            t.Add("binary.>", (object f, string a, string b) => String.CompareOrdinal(a, b) > 0, doNullProp: true);
            t.Add("binary.>", (object f, PartialDateTime a, PartialDateTime b) => a > b, doNullProp: true);
            t.Add("binary.>", (object f, PartialTime a, PartialTime b) => a > b, doNullProp: true);
            t.Add("binary.>", (object f, Quantity a, Quantity b) => a > b, doNullProp:true);

            t.Add("binary.<", (object f, long a, long b) => a < b, doNullProp: true);
            t.Add("binary.<", (object f, decimal a, decimal b) => a < b, doNullProp: true);
            t.Add("binary.<", (object f, string a, string b) => String.CompareOrdinal(a, b) < 0, doNullProp: true);
            t.Add("binary.<", (object f, PartialDateTime a, PartialDateTime b) => a < b, doNullProp: true);
            t.Add("binary.<", (object f, PartialTime a, PartialTime b) => a < b, doNullProp: true);
            t.Add("binary.<", (object f, Quantity a, Quantity b) => a < b, doNullProp: true);

            t.Add("binary.<=", (object f, long a, long b) => a <= b, doNullProp: true);
            t.Add("binary.<=", (object f, decimal a, decimal b) => a <= b, doNullProp: true);
            t.Add("binary.<=", (object f, string a, string b) => String.CompareOrdinal(a, b) <= 0, doNullProp: true);
            t.Add("binary.<=", (object f, PartialDateTime a, PartialDateTime b) => a <= b, doNullProp: true);
            t.Add("binary.<=", (object f, PartialTime a, PartialTime b) => a <= b, doNullProp: true);
            t.Add("binary.<=", (object f, Quantity a, Quantity b) => a <= b, doNullProp:true);

            t.Add("binary.>=", (object f, long a, long b) => a >= b, doNullProp: true);
            t.Add("binary.>=", (object f, decimal a, decimal b) => a >= b, doNullProp: true);
            t.Add("binary.>=", (object f, string a, string b) => String.CompareOrdinal(a, b) >= 0, doNullProp: true);
            t.Add("binary.>=", (object f, PartialDateTime a, PartialDateTime b) => a >= b, doNullProp: true);
            t.Add("binary.>=", (object f, PartialTime a, PartialTime b) => a >= b, doNullProp: true);
            t.Add("binary.>=", (object f, Quantity a, Quantity b) => a >= b, doNullProp: true);

            t.Add("single", (IEnumerable<ITypedElement> f) => f.Single(), doNullProp: true);
            t.Add("skip", (IEnumerable<ITypedElement> f, long a) =>  f.Skip((int)a), doNullProp: true);
            t.Add("first", (IEnumerable<ITypedElement> f) => f.First(), doNullProp: true);
            t.Add("last", (IEnumerable<ITypedElement> f) => f.Last(), doNullProp: true);
            t.Add("tail", (IEnumerable<ITypedElement> f) => f.Tail(), doNullProp: true);
            t.Add("take", (IEnumerable<ITypedElement> f, long a) => f.Take((int)a), doNullProp: true);
            t.Add("builtin.item", (IEnumerable<ITypedElement> f, long a) => f.Item((int)a), doNullProp: true);

            t.Add("toBoolean", (ITypedElement f) => f.ToBoolean(), doNullProp: true);
            t.Add("convertsToBoolean", (ITypedElement f) => f.ConvertsToBoolean(), doNullProp: true);
            t.Add("toInteger", (ITypedElement f) => f.ToInteger(), doNullProp: true);
            t.Add("convertsToInteger", (ITypedElement f) => f.ConvertsToInteger(), doNullProp: true);
            t.Add("toDecimal", (ITypedElement f) => f.ToDecimal(), doNullProp: true);
            t.Add("convertsToDecimal", (ITypedElement f) => f.ConvertsToDecimal(), doNullProp: true);
            t.Add("toDateTime", (ITypedElement f) => f.ToDateTime(), doNullProp: true);
            t.Add("convertsToDateTime", (ITypedElement f) => f.ConvertsToDateTime(), doNullProp: true);
            t.Add("toTime", (ITypedElement f) => f.ToTime(), doNullProp: true);
            t.Add("convertsToTime", (ITypedElement f) => f.ConvertsToTime(), doNullProp: true);
            t.Add("toString", (ITypedElement f) => f.ToStringRepresentation(), doNullProp: true);
            t.Add("convertsToString", (ITypedElement f) => f.ConvertsToString(), doNullProp: true);
            t.Add("toQuantity", (ITypedElement f) => f.ToQuantity(), doNullProp: true);
            t.Add("convertsToQuantity", (ITypedElement f) => f.ConvertsToQuantity(), doNullProp: true);

            t.Add("upper", (string f) => f.ToUpper(), doNullProp: true);
            t.Add("lower", (string f) => f.ToLower(), doNullProp: true);
            t.Add("toChars", (string f) => f.ToChars(), doNullProp: true);
            t.Add("substring", (string f, long a) => f.FpSubstring((int)a), doNullProp: true);
            t.Add("substring", (string f, long a, long b) => f.FpSubstring((int)a, (int)b), doNullProp: true);
            t.Add("startsWith", (string f, string fragment) => f.StartsWith(fragment), doNullProp: true);
            t.Add("endsWith", (string f, string fragment) => f.EndsWith(fragment), doNullProp: true);
            t.Add("matches", (string f, string regex) => Regex.IsMatch(f, regex), doNullProp: true);
            t.Add("indexOf", (string f, string fragment) => f.FpIndexOf(fragment), doNullProp: true);
            t.Add("contains", (string f, string fragment) => f.Contains(fragment), doNullProp: true);
            t.Add("replaceMatches", (string f, string regex, string subst) => Regex.Replace(f, regex, subst), doNullProp: true);
            t.Add("replace", (string f, string regex, string subst) => f.FpReplace(regex, subst), doNullProp: true);
            t.Add("length", (string f) => f.Length, doNullProp: true);

            // The next two functions existed pre-normative, so we have kept them.
            t.Add("is", (ITypedElement f, string name) => f.Is(name), doNullProp: true);
            t.Add("as", (IEnumerable<ITypedElement> f, string name) => f.FilterType(name), doNullProp: true);

            t.Add("ofType", (IEnumerable<ITypedElement> f, string name) => f.FilterType(name), doNullProp: true);
            t.Add("binary.is", (object f, ITypedElement left, string name) => left.Is(name), doNullProp: true);
            t.Add("binary.as", (object f, ITypedElement left, string name) => left.CastAs(name), doNullProp: true);
            // [2019-01-11 EK] Commented out as this use is not according to the normative spec.
            //t.Add("binary.as", (object f, IEnumerable<ITypedElement> left, string name) => left.FilterType(name), doNullProp: true);

            t.Add("extension", (IEnumerable<ITypedElement> f, string url) => f.Extension(url), doNullProp: true);

            // Logic operators do not use null propagation and may do short-cut eval
            t.AddLogic("binary.and", (a, b) => a.And(b));
            t.AddLogic("binary.or", (a, b) => a.Or(b));
            t.AddLogic("binary.xor", (a, b) => a.XOr(b));
            t.AddLogic("binary.implies", (a, b) => a.Implies(b));

            // Special late-bound functions
            t.AddFunction(new CallSignature("where", typeof(IEnumerable<ITypedElement>), typeof(object), typeof(Invokee)), runWhere);
            t.AddFunction(new CallSignature("select", typeof(IEnumerable<ITypedElement>), typeof(object), typeof(Invokee)), runSelect);
            t.AddFunction(new CallSignature("all", typeof(bool), typeof(object), typeof(Invokee)), runAll);
            t.AddFunction(new CallSignature("any", typeof(bool), typeof(object), typeof(Invokee)), runAny);
            t.AddFunction(new CallSignature("repeat", typeof(IEnumerable<ITypedElement>), typeof(object), typeof(Invokee)), runRepeat);
            t.AddFunction(new CallSignature("trace", typeof(IEnumerable<ITypedElement>), typeof(string), typeof(object), typeof(Invokee)), Trace);


            t.AddConst("sct", "http://snomed.info/sct");
            t.AddConst("loinc", "http://loinc.org");
            t.AddConst("ucum", "http://unitsofmeasure.org");

            t.Add("builtin.coreexturl", (object f, string id) => getCoreExtensionUrl(id));
            t.Add("builtin.corevsurl", (object f, string id) => getCoreValueSetUrl(id));

            return t;
        }


        private static string getCoreExtensionUrl(string id)
        {
            return "http://hl7.org/fhir/StructureDefinition/" + id;
        }

        private static string getCoreValueSetUrl(string id)
        {
            return "http://hl7.org/fhir/ValueSet/" + id;
        }

        private static IEnumerable<ITypedElement> Trace(EvaluationContext ctx, IEnumerable<Invokee> arguments)
        {
            var focus = arguments.First()(ctx, InvokeeFactory.EmptyArgs);
            string name = arguments.Skip(1).First()(ctx, InvokeeFactory.EmptyArgs).FirstOrDefault()?.Value as string;

            List<Invokee> selectArgs = new List<Invokee>();
            selectArgs.Add(arguments.First());
            selectArgs.AddRange(arguments.Skip(2));
            var selectResults = runSelect(ctx, selectArgs);
            ctx?.Tracer?.Invoke(name, selectResults);

            return focus;
        }

        private static IEnumerable<ITypedElement> runWhere(EvaluationContext ctx, IList<Invokee> arguments)
        {
            var focus = arguments[0](ctx, InvokeeFactory.EmptyArgs);
            var lambda = arguments[1];

            foreach (ITypedElement element in focus)
            {
                var newFocus = InvokeeFactory.Return(element);
                if (lambda(ctx, new List<Invokee> { newFocus }).BooleanEval() == true)
                    yield return element;
            }
        }

        private static IEnumerable<ITypedElement> runSelect(EvaluationContext ctx, IList<Invokee> arguments)
        {
            var focus = arguments[0](ctx, InvokeeFactory.EmptyArgs);
            var lambda = arguments[1];

            foreach (ITypedElement element in focus)
            {
                var newFocus = InvokeeFactory.Return(element);
                var result = lambda(ctx, new List<Invokee> { newFocus } );
                foreach (var resultElement in result)       // implement SelectMany()
                    yield return resultElement;
            }
        }

        private static IEnumerable<ITypedElement> runRepeat(EvaluationContext ctx, IList<Invokee> arguments)
        {
            var focus = arguments[0](ctx, InvokeeFactory.EmptyArgs);
            var lambda = arguments[1];

            var fullResult = new List<ITypedElement>();
            List<ITypedElement> newNodes = new List<ITypedElement>(focus);

            while (newNodes.Any())
            {
                var current = newNodes;
                newNodes = new List<ITypedElement>();

                foreach (ITypedElement element in current)
                {
                    var newFocus = InvokeeFactory.Return(element);
                    newNodes.AddRange(lambda(ctx, new List<Invokee> { newFocus }));
                }

                fullResult.AddRange(newNodes);
            }

            return fullResult;
        }

        private static IEnumerable<ITypedElement> runAll(EvaluationContext ctx, IList<Invokee> arguments)
        {
            var focus = arguments[0](ctx, InvokeeFactory.EmptyArgs);
            var lambda = arguments[1];

            foreach (ITypedElement element in focus)
            {
                var newFocus = InvokeeFactory.Return(element);

                var result = lambda(ctx, new List<Invokee> { newFocus }).BooleanEval();
                if (result == null) return FhirValueList.Empty;
                if (result == false) return FhirValueList.Create(false);
            }

            return FhirValueList.Create(true);
        }

        private static IEnumerable<ITypedElement> runAny(EvaluationContext ctx, IList<Invokee> arguments)
        {
            var focus = arguments[0](ctx, InvokeeFactory.EmptyArgs);
            var lambda = arguments[1];

            foreach (ITypedElement element in focus)
            {
                var newFocus = InvokeeFactory.Return(element);
                var result = lambda(ctx, new List<Invokee> { newFocus }).BooleanEval();

                //if (result == null) return FhirValueList.Empty; -> otherwise this would not be where().exists()
                //Patient.identifier.any(use = 'official') would return {} if ANY identifier has no 'use' element. Unexpected behaviour, I think
                if (result == true) return FhirValueList.Create(true);
            }

            return FhirValueList.Create(false);
        }
    }
}
