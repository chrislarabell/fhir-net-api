﻿/* 
 * Copyright (c) 2015, Firely (info@fire.ly) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/fhir-net-api/master/LICENSE
 */

// To introduce the DSTU2 FHIR specification
// extern alias dstu2;

using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Model.Primitives;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using Hl7.FhirPath.Expressions;
using Hl7.FhirPath.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Hl7.FhirPath.R4.Tests
{
    public class PatientFixture : IDisposable
    {
        public ITypedElement TestInput;
        public ITypedElement Questionnaire;
        public ITypedElement UuidProfile;
        public int Counter = 0;
        public XDocument Xdoc;

        public PatientFixture()
        {
            var parser = new FhirXmlParser();
            var tpXml = TestData.ReadTextFile("fp-test-patient.xml");

            var patient = parser.Parse<Patient>(tpXml);
            TestInput = patient.ToTypedElement();

            tpXml = TestData.ReadTextFile("questionnaire-example.xml");
            var quest = parser.Parse<Questionnaire>(tpXml);
            Questionnaire = quest.ToTypedElement();

            tpXml = TestData.ReadTextFile("uuid.profile.xml");
            var uuid = parser.Parse<StructureDefinition>(tpXml);
            UuidProfile = uuid.ToTypedElement();

            Xdoc = new XDocument(new XElement("group", new XAttribute("name", "CSharpTests")));
        }

        public void Save(XDocument document, string filename)
        {
            using (var file = new FileStream(filename, FileMode.Create))
            using (var writer = new StreamWriter(file))
            {
                Xdoc.Save(writer);
            }
        }

        public void Dispose()
        {
            Directory.CreateDirectory(@"c:\temp");
            Save(Xdoc, @"c:\temp\csharp-tests.xml");
        }

        public void IsTrue(string expr)
        {
            Counter += 1;
            var testName = "CSharpTest" + Counter.ToString("D4");
            var fileName = "fp-test-patient.xml";

            var testXml = new XElement("test",
                        new XAttribute("name", testName), new XAttribute("inputfile", fileName),
                        new XElement("expression", new XText(expr)),
                        new XElement("output", new XAttribute("type", "boolean"), new XText("true")));
            Xdoc.Elements().First().Add(testXml);

            Assert.IsTrue(TestInput.IsBoolean(expr, true));
        }

        public void IsTrue(string expr, ITypedElement input)
        {
            Assert.IsTrue(input.IsBoolean(expr, true));
        }
    }

    [TestClass]
    public class FhirPathEvaluatorTest
    {
        static PatientFixture fixture;

        [ClassInitialize]
        public static void Initialize(TestContext ctx)
        {
            ElementNavFhirExtensions.PrepareFhirSymbolTableFunctions();
            fixture = new PatientFixture();
        }

        [TestMethod]
        public void TestTreeVisualizerVisitor()
        {
            var compiler = new FhirPathCompiler();
            var expr = compiler.Parse("doSomething('ha!', 4, {}, $this, somethingElse(true))");
            var result = expr.Dump();
            Debug.WriteLine(result);
        }

        [TestMethod]
        public void TestExistence()
        {
            fixture.IsTrue(@"{}.empty()");
            fixture.IsTrue(@"1.empty().not()");
            fixture.IsTrue(@"1.exists()");
            fixture.IsTrue(@"Patient.identifier.exists()");
            fixture.IsTrue(@"Patient.dientifeir.exists().not()");
            fixture.IsTrue(@"Patient.telecom.rank.exists()");
            Assert.AreEqual(3, fixture.TestInput.Scalar(@"identifier.count()"));
            Assert.AreEqual(3, fixture.TestInput.Scalar(@"Patient.identifier.count()"));
            Assert.AreEqual(3, fixture.TestInput.Scalar(@"Patient.identifier.value.count()"));
            Assert.AreEqual(1, fixture.TestInput.Scalar(@"Patient.telecom.rank"));
            fixture.IsTrue(@"Patient.telecom.rank = 1");
        }

        [TestMethod]
        public void TestNullPropagation()
        {
            fixture.IsTrue(@"({}.substring(0)).empty()");
            fixture.IsTrue(@"('hello'.substring({})).empty()");
        }

        [TestMethod]
        public void TestDynaBinding()
        {
#pragma warning disable CS0618 // Type or member is internal
            var input = SourceNode.Node("root",
                    SourceNode.Valued("child", "Hello world!"),
                    SourceNode.Valued("child", "4")).ToTypedElement();
#pragma warning restore CS0618 // Type or member is internal

            Assert.AreEqual("ello", input.Scalar(@"$this.child[0].substring(1,%context.child[1].toInteger())"));
        }


        [TestMethod]
        public void TestSDF11Bug()
        {
            Assert.IsTrue(fixture.UuidProfile.IsBoolean("snapshot.element.first().path = type", true));
        }

        [TestMethod]
        public void TestSubsetting()
        {
            fixture.IsTrue(@"Patient.identifier[1] != Patient.identifier.first()");

            fixture.IsTrue(@"Patient.identifier[0] = Patient.identifier.first()");
            fixture.IsTrue(@"Patient.identifier[2] = Patient.identifier.last()");
            fixture.IsTrue(@"Patient.identifier[0] | Patient.identifier[1]  = Patient.identifier.take(2)");
            fixture.IsTrue(@"Patient.identifier.skip(1) = Patient.identifier.tail()");
            fixture.IsTrue(@"Patient.identifier.skip(2) = Patient.identifier.last()");
            fixture.IsTrue(@"Patient.identifier.first().single()");

            try
            {
                fixture.IsTrue(@"Patient.identifier.single()");
                // todo: mh
                // Assert.Fail();
                throw new Exception();
            }
            catch (InvalidOperationException io)
            {
                Assert.IsTrue(io.Message.Contains("contains more than one element"));
            }
        }

        [TestMethod]
        public void TestGreaterThan()
        {
            fixture.IsTrue(@"4.5 > 0");
            fixture.IsTrue(@"'ewout' > 'alfred'");
            fixture.IsTrue(@"2016-04-01 > 2015-04-01");
            fixture.IsTrue(@"5 > 6 = false");
            fixture.IsTrue(@"(5 > {}).empty()");
        }

        [TestMethod]
        public void TestMath()
        {
            fixture.IsTrue(@"-4.5 + 4.5 = 0");
            fixture.IsTrue(@"4/2 = 2");
            fixture.IsTrue(@"2/4 = 0.5");
            fixture.IsTrue(@"10/4 = 2.5");
            fixture.IsTrue(@"10.0/4 = 2.5");
            fixture.IsTrue(@"4.0/2.0 = 2");
            fixture.IsTrue(@"2.0/4 = 0.5");
            fixture.IsTrue(@"2.0 * 4 = 8");
            fixture.IsTrue(@"2 * 4.1 = 8.2");
            fixture.IsTrue(@"-0.5 * 0.5 = -0.25");
            fixture.IsTrue(@"5 - 4.5 = 0.5");
            fixture.IsTrue(@"9.5 - 4.5 = 5");
            fixture.IsTrue(@"5 + 4.5 = 9.5");
            fixture.IsTrue(@"9.5 + 0.5 = 10");

            fixture.IsTrue(@"103 mod 5 = 3");
            fixture.IsTrue(@"101.4 mod 5.2 = 2.6");
            fixture.IsTrue(@"103 div 5 = 20");
            fixture.IsTrue(@"20.0 div 5.5 = 3");

            fixture.IsTrue(@"'offic'+'ial' = 'official'");

            fixture.IsTrue(@"12/(2+2) - (3 div 2) = 2");
            fixture.IsTrue(@"-4.5 + 4.5 * 2 * 4 / 4 - 1.5 = 3");
        }


        [TestMethod]
        public void Test3VLBoolean()
        {
            fixture.IsTrue(@"true and true");
            fixture.IsTrue(@"(true and false) = false");
            fixture.IsTrue(@"(true and {}).empty()");
            fixture.IsTrue(@"(false and true) = false");
            fixture.IsTrue(@"(false and false) = false");
            fixture.IsTrue(@"(false and {}) = false");
            fixture.IsTrue(@"({} and true).empty()");
            fixture.IsTrue(@"({} and false) = false");
            fixture.IsTrue(@"({} and {}).empty()");

            fixture.IsTrue(@"true or true");
            fixture.IsTrue(@"true or false");
            fixture.IsTrue(@"true or {}");
            fixture.IsTrue(@"false or true");
            fixture.IsTrue(@"(false or false) = false");
            fixture.IsTrue(@"(false or {}).empty()");
            fixture.IsTrue(@"{} or true");
            fixture.IsTrue(@"({} or false).empty()");
            fixture.IsTrue(@"({} or {}).empty()");

            fixture.IsTrue(@"(true xor true)=false");
            fixture.IsTrue(@"true xor false");
            fixture.IsTrue(@"(true xor {}).empty()");
            fixture.IsTrue(@"false xor true");
            fixture.IsTrue(@"(false xor false) = false");
            fixture.IsTrue(@"(false xor {}).empty()");
            fixture.IsTrue(@"({} xor true).empty()");
            fixture.IsTrue(@"({} xor false).empty()");
            fixture.IsTrue(@"({} xor {}).empty()");

            fixture.IsTrue(@"true implies true");
            fixture.IsTrue(@"(true implies false) = false");
            fixture.IsTrue(@"(true implies {}).empty()");
            fixture.IsTrue(@"false implies true");
            fixture.IsTrue(@"false implies false");
            fixture.IsTrue(@"false implies {}");
            fixture.IsTrue(@"{} implies true");
            fixture.IsTrue(@"({} implies false).empty()");
            fixture.IsTrue(@"({} implies {}).empty()");
        }

        [TestMethod]
        public void TestLogicalShortcut()
        {
            fixture.IsTrue(@"true or (1/0 = 0)");
            fixture.IsTrue(@"(false and (1/0 = 0)) = false");
        }


        [TestMethod]
        public void TestConversions()
        {
            fixture.IsTrue(@"(4.1).toString() = '4.1'");
            fixture.IsTrue(@"true.toString() = 'true'");
            fixture.IsTrue(@"true.toDecimal() = 1");
            fixture.IsTrue(@"Patient.identifier.value.first().toDecimal() = 654321");
            fixture.IsTrue(@"@2014-12-14T.toString() = '2014-12-14'");
            fixture.IsTrue(@"@2014-12-14.toString() = '2014-12-14'");
        }

        [TestMethod]
        public void TestIIf()
        {
            fixture.IsTrue(@"Patient.name.iif(exists(), 'named', 'unnamed') = 'named'");
            fixture.IsTrue(@"Patient.name.iif(empty(), 'unnamed', 'named') = 'named'");

            fixture.IsTrue(@"Patient.contained[0].name.iif(exists(), 'named', 'unnamed') = 'named'");
            fixture.IsTrue(@"Patient.contained[0].name.iif(empty(), 'unnamed', 'named') = 'named'");

            fixture.IsTrue(@"Patient.name.iif({}, 'named', 'unnamed') = 'unnamed'");

            //   fixture.IsTrue(@"Patient.name[0].family.iif(length()-8 != 0, 5/(length()-8), 'no result') = 'no result'");
        }

        [TestMethod]
        public void TestExtension()
        {
            fixture.IsTrue(@"Patient.birthDate.extension('http://hl7.org/fhir/StructureDefinition/patient-birthTime').exists()");
            fixture.IsTrue(@"Patient.birthDate.extension(%""ext-patient-birthTime"").exists()");
            fixture.IsTrue(@"Patient.birthDate.extension('http://hl7.org/fhir/StructureDefinition/patient-birthTime1').empty()");
        }

        [TestMethod]
        public void TestEquality()
        {
            fixture.IsTrue(@"4 = 4");
            fixture.IsTrue(@"4 = 4.0");
            fixture.IsTrue(@"true = true");
            fixture.IsTrue(@"true != false");

            fixture.IsTrue(@"Patient.identifier = Patient.identifier");
            fixture.IsTrue(@"Patient.identifier.first() != Patient.identifier.skip(1)");
            fixture.IsTrue(@"(1|2|3) = (1|2|3)");
            fixture.IsTrue(@"(1|2|3) = (1.0|2.0|3)");
            fixture.IsTrue(@"(1|Patient.identifier|3) = (1|Patient.identifier|3)");
            fixture.IsTrue(@"(3|Patient.identifier|1) != (1|Patient.identifier|3)");

            fixture.IsTrue(@"Patient.gender = 'male'"); // gender has an extension
            fixture.IsTrue(@"Patient.communication = Patient.communication");       // different extensions, same values
            fixture.IsTrue(@"Patient.communication.first() = Patient.communication.skip(1)");       // different extensions, same values
        }

        [TestMethod, Ignore]
        public void TestDateTimeEquality()
        {
            fixture.IsTrue(@"@2015-01-01 = @2015-01-01");
            fixture.IsTrue(@"@2015-01-01T = @2015-01-01T");
            fixture.IsTrue(@"@2015-01-01 != @2015-01");
            fixture.IsTrue(@"@2015-01-01T != @2015-01T");

            fixture.IsTrue(@"@2015-01-01T13:40:50+00:00 = @2015-01-01T13:40:50Z");

            fixture.IsTrue(@"@T13:45:02 = @T13:45:02");
            fixture.IsTrue(@"@T13:45:02 != @T14:45:02");
        }

        [TestMethod]
        public void TestCollectionFunctions()
        {
            fixture.IsTrue(@"Patient.identifier.use.distinct() = ('usual' | 'official')");
            fixture.IsTrue(@"Patient.identifier.use.distinct().count() = 2");
            fixture.IsTrue(@"Patient.identifier.use.isDistinct() = false");
            fixture.IsTrue(@"Patient.identifier.system.isDistinct()");
            fixture.IsTrue(@"(3|4).isDistinct()");
            fixture.IsTrue(@"{}.isDistinct()");

            fixture.IsTrue(@"Patient.identifier.skip(1).subsetOf(%context.Patient.identifier)");
            fixture.IsTrue(@"Patient.identifier.supersetOf(%context.Patient.identifier)");
            fixture.IsTrue(@"Patient.identifier.supersetOf({})");
            fixture.IsTrue(@"{}.subsetOf(%context.Patient.identifier)");
        }

        [TestMethod]
        public void TestCollectionOperators()
        {
            fixture.IsTrue(@"Patient.identifier.last() in Patient.identifier");
            fixture.IsTrue(@"4 in (3|4.0|5)");
            fixture.IsTrue(@"(3|4.0|5|3).count() = 3");
            fixture.IsTrue(@"Patient.identifier contains Patient.identifier.last()");
            fixture.IsTrue(@"(3|4.0|5) contains 4");
            fixture.IsTrue(@"({} contains 4) = false");
            fixture.IsTrue(@"(4 in {}) = false");
            fixture.IsTrue(@"Patient.identifier.count() = (Patient.identifier | Patient.identifier).count()");
            fixture.IsTrue(@"(Patient.identifier | Patient.name).count() = Patient.identifier.count() + Patient.name.count()");
            fixture.IsTrue(@"Patient.select(identifier | name).count() = Patient.select(identifier.count() + name.count())");
        }


        [TestMethod, Ignore]
        public void TestDateTimeEquivalence()
        {
            fixture.IsTrue("@2012-04-15T ~ @2012-04-15T10:00:00");
            fixture.IsTrue("@T10:01:02 !~ @T10:01:55+01:00");
        }

        public static string ToString(ITypedElement nav)
        {
            var result = nav.Name;

            if (nav.InstanceType != null)
            {
                result += ": " + nav.InstanceType;
            }

            if (nav.Value != null) result += " = " + nav.Value;

            return result;
        }

        //public static void Render(IElementNavigator navigator, int nest = 0)
        //{
        //    do
        //    {
        //        string indent = new string(' ', nest * 4);
        //        Debug.WriteLine($"{indent}" + ToString(navigator));

        //        var child = navigator.Clone();
        //        if (child.MoveToFirstChild())
        //        {
        //            Render(child, nest + 1);
        //        }
        //    }
        //    while (navigator.MoveToNext());
        //}


        [TestMethod]
        public void TestWhere()
        {
            fixture.IsTrue("Patient.identifier.where(use = ('offic' + 'ial')).count() = 2");

            fixture.IsTrue(@"(5 | 'hi' | 4).where($this = 'hi').count()=1");
            fixture.IsTrue(@"{}.where($this = 'hi').count()=0");
        }

        [TestMethod]
        public void TestAll()
        {
            fixture.IsTrue(@"Patient.identifier.skip(1).all(use = 'official')");
            fixture.IsTrue(@"Patient.identifier.skip(999).all(use = 'official')");   // {}.All() aways returns true
            fixture.IsTrue(@"Patient.identifier.skip(1).all({}).empty()");   // empty results still count as "empty"
        }

        [TestMethod]
        public void TestAny()
        {
            fixture.IsTrue(@"Patient.identifier.any(use = 'official')");
            fixture.IsTrue(@"Patient.identifier.skip(999).any(use = 'official') = false");   // {}.Any() aways returns true
            fixture.IsTrue(@"Patient.contained.skip(1).item.any(code.code = 'COMORBIDITY')");       // really need to filter on Questionnare (as('Questionnaire'))
        }

        [TestMethod]
        public void TestRepeat()
        {
            fixture.IsTrue(@"Patient.contained.skip(1).repeat(item.where(type='group')).count() = 3");       // really need to filter on Questionnare (as('Questionnaire'))
            fixture.IsTrue(@"Patient.contained.skip(1).repeat(item).count() = 10");       // really need to filter on Questionnare (as('Questionnaire'))
            fixture.IsTrue(@"Patient.contained.skip(1).repeat(item | item.where(type='group')).count() = 10");       // really need to filter on Questionnare (as('Questionnaire'))
            fixture.IsTrue(@"Patient.contained.skip(1).repeat(item ).count() = 10");       // really need to filter on Questionnare (as('Questionnaire'))
            fixture.IsTrue(@"Patient.contained.skip(1).repeat(item).select(code.code) contains 'COMORBIDITY'");       // really need to filter on Questionnare (as('Questionnaire'))
            fixture.IsTrue(@"Patient.contained.skip(1).repeat(item).any(code.code = 'COMORBIDITY')");       // really need to filter on Questionnare (as('Questionnaire'))
            fixture.IsTrue(@"Patient.contained.skip(1).repeat(item.where(type='group')).any(code.code = 'CARDIAL') = false");       // really need to filter on Questionnare (as('Questionnaire'))
            fixture.IsTrue(@"Patient.contained.skip(1).repeat(item).any(code.code = 'CARDIAL')");       // really need to filter on Questionnare (as('Questionnaire'))

            fixture.IsTrue(@"Questionnaire.descendants().linkId.distinct()", fixture.Questionnaire);
            fixture.IsTrue(@"Questionnaire.repeat(item).code.count()", fixture.Questionnaire);
        }


        [TestMethod]
        public void TestExpression()
        {
            fixture.IsTrue(@"(Patient.identifier.where( use = ( 'offic' + 'ial')) = 
                       Patient.identifier.skip(8 div 2 - 3*2 + 3)) and (Patient.identifier.where(use='usual') = 
                        Patient.identifier.first())");

            fixture.IsTrue(@"(1|2|3|4|5).where($this > 2 and $this <= 4) = (3|4)");

            fixture.IsTrue(@"(1|2|2|3|Patient.identifier.first()|Patient.identifier).distinct().count() = 
                        3 + Patient.identifier.count()");

            fixture.IsTrue(@"(Patient.identifier.where(use='official').last() in Patient.identifier) and
                       (Patient.identifier.first() in Patient.identifier.tail()).not()");

            fixture.IsTrue(@"Patient.identifier.any(use='official') and identifier.where(use='usual').exists()");

            fixture.IsTrue(@"Patient.descendants().where($this.as(string).contains('222'))[1] = %context.contained.address.line");

            fixture.IsTrue(@"Patient.name.select(given|family).count() = 2");
            fixture.IsTrue(@"Patient.identifier.where(use = 'official').select(value + 'yep') = ('7654321yep' | '11223344yep')");
            fixture.IsTrue(@"Patient.descendants().where(($this is code) and ($this.contains('wne'))).trace('them') = contact.relationship.coding.code");
            fixture.IsTrue(@"Patient.descendants().as(code).where(matches('i.*/gif')) in Patient.photo.children()");

            fixture.IsTrue(
                @"'m' + gender.extension('http://example.org/StructureDefinition/real-gender').value.as(code)
                    .substring(1,4) + 
                    gender.extension('http://example.org/StructureDefinition/real-gender').value.as(code)
                    .substring(5) = 'metrosexual'");

            fixture.IsTrue(
                    @"gender.extension('http://example.org/StructureDefinition/real-gender').value.as(code)
                    .select('m' + $this.substring(1,4) + $this.substring(5)) = 'metrosexual'");

        }

        [TestMethod]
        public void TestExpressionTodayFunction()
        {
            // Check that date comes in
            Assert.AreEqual(PartialDate.Today(), fixture.TestInput.Scalar("today()"));

            // Check greater than
            fixture.IsTrue("today() < @" + PartialDate.FromDateTimeOffset(DateTimeOffset.UtcNow.AddDays(1), includeOffset: false));

            // Check less than
            fixture.IsTrue("today() > @" + PartialDate.FromDateTimeOffset(DateTimeOffset.UtcNow.AddDays(-1), includeOffset: false));

            // Check ==
            fixture.IsTrue("today() = @" + PartialDate.Today());

            // This unit-test will fail if you are working between midnight
            // and start-of-day in GMT:
            // e.g. 2018-08-10T01:00T+02:00 > 2018-08-10 will fail, which is then
            // test on the next line
            //fixture.IsTrue("now() > @" + PartialDateTime.Today());
            fixture.IsTrue("now() >= @" + PartialDateTime.Now());
        }

        [TestMethod]
        public void TestSubstring()
        {
            fixture.IsTrue("Patient.name.family");

            fixture.IsTrue("Patient.name.family.substring(0,6) = 'Donald'");
            fixture.IsTrue("Patient.name.family.substring(2,6) = 'nald'");
            fixture.IsTrue("Patient.name.family.substring(2,4) = 'nald'");

            fixture.IsTrue("Patient.name.family.substring(2,length()-3) = 'nal'");

            fixture.IsTrue("Patient.name.family.substring(-1,8).empty()");
            fixture.IsTrue("Patient.name.family.substring(999,1).empty()");
            fixture.IsTrue("''.substring(0,1).empty()");
            fixture.IsTrue("{}.substring(0,10).empty()");
            fixture.IsTrue("{}.substring(0,10).empty()");

            try
            {
                // TODO: Improve exception on this one
                fixture.IsTrue("Patient.identifier.use.substring(0,10)");
                // todo: mh: Assert.Fail();
                throw new Exception();
            }
            catch (InvalidOperationException)
            {
            }
        }

        [TestMethod]
        public void TestStringOps()
        {
            fixture.IsTrue("Patient.name.family.startsWith('')");
            fixture.IsTrue("Patient.name.family.startsWith('Don')");
            fixture.IsTrue("Patient.name.family.startsWith('Dox')=false");

            fixture.IsTrue("Patient.name.family.endsWith('')");
            fixture.IsTrue("Patient.name.family.endsWith('ald')");
            fixture.IsTrue("Patient.name.family.endsWith('old')=false");

            fixture.IsTrue("Patient.identifier.where(system='urn:oid:0.1.2.3.4.5.6.7').value.matches('^[1-6]+$')");
            fixture.IsTrue("Patient.identifier.where(system='urn:oid:0.1.2.3.4.5.6.7').value.matches('^[1-3]+$') = false");

            fixture.IsTrue("Patient.contained.name[0].family.indexOf('ywo') = 4");
            fixture.IsTrue("Patient.contained.name[0].family.indexOf('') = 0");
            fixture.IsTrue("Patient.contained.name[0].family.indexOf('qq') = -1");

            fixture.IsTrue("Patient.contained.name[0].family.contains('ywo')");
            fixture.IsTrue("Patient.contained.name[0].family.contains('ywox')=false");
            fixture.IsTrue("Patient.contained.name[0].family.contains('')");

            fixture.IsTrue(@"'11/30/1972'.replaceMatches('\\b(?<month>\\d{1,2})/(?<day>\\d{1,2})/(?<year>\\d{2,4})\\b','${day}-${month}-${year}') = '30-11-1972'");

            fixture.IsTrue(@"'abc'.replace('a', 'q') = 'qbc'");
            fixture.IsTrue(@"'abc'.replace('a', 'qq') = 'qqbc'");
            fixture.IsTrue(@"'abc'.replace('q', 'x') = 'abc'");
            fixture.IsTrue(@"'abc'.replace('ab', '') = 'c'");
            fixture.IsTrue(@"'abc'.replace('', 'xh') = 'xhaxhbxhc'");

            fixture.IsTrue("Patient.contained.name[0].family.length() = " + "Everywoman".Length);
            fixture.IsTrue("''.length() = 0");
            fixture.IsTrue("{}.length().empty()");
        }


        //[TestMethod]
        //public void TestUnionNotDistinct()
        //{
        //    var patXml = @"<Patient xmlns='http://hl7.org/fhir'>
        //        <name>
        //            <given value='bobs' />
        //            <given value='bobs' />
        //            <given value='bob2' />
        //            <family value='f1' />
        //            <family value='f2' />
        //        </name>
        //        <birthDate value='1973' />
        //    </Patient>";

        //    var pat = (new FhirXmlParser()).Parse<Patient>(patXml);
        //    var patNav = new PocoNavigator(pat);

        //    var result = PathExpression.Select("name.given | name.family", new[] { patNav });
        //    Assert.Equal(5, result.Count());
        //}

        [TestMethod]
        public void CompilationIsCached()
        {
            Stopwatch sw = new Stopwatch();
            string expression = "";

            sw.Start();

            var random = new Random();

            // something that has not been compiled before
            for (int i = 0; i < 1000; i++)
            {
                var next = random.Next(0, 10000);
                expression = $"Patient.name[{next}]";
                fixture.TestInput.Select(expression);
            }
            sw.Stop();

            var uncached = sw.ElapsedMilliseconds;

            sw.Restart();

            for (int i = 0; i < 1000; i++)
            {
                fixture.TestInput.Select(expression);
            }

            sw.Stop();

            var cached = sw.ElapsedMilliseconds;
            Console.WriteLine("Uncached: {0}, cached: {1}".FormatWith(uncached, cached));

            Assert.IsTrue(cached < uncached / 2);
        }
    }
}