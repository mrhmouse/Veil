﻿using System.Collections.Generic;
using NUnit.Framework;
using Veil.Parser;
using E = Veil.Parser.Expression;
using S = Veil.Parser.SyntaxTree;

namespace Veil.Compiler
{
    [TestFixture]
    internal class ExtendTests : CompilerTestBase
    {
        [Test]
        public void Should_be_able_to_extend_a_template()
        {
            var model = new { Name = "Bob" };
            RegisterTemplate("master", S.Block(
                S.WriteString("<html><head>"),
                S.Override("head"),
                S.WriteString("</head></html>")
            ));
            var template = S.Extend("master", new Dictionary<string, SyntaxTreeNode> {
                {
                    "head",
                    S.Block(
                        S.WriteString("<title>"),
                        S.WriteExpression(E.Property(model.GetType(), "Name")),
                        S.WriteString("</title>")
                    )
                }
            });
            var result = ExecuteTemplate(template, model);

            Assert.That(result, Is.EqualTo("<html><head><title>Bob</title></head></html>"));
        }

        [Test]
        public void Should_not_throw_if_optional_override_is_missing()
        {
            var model = new { };
            RegisterTemplate("master", S.Block(S.WriteString("Hello"), S.Override("foo", true)));
            var template = S.Extend("master");
            var result = ExecuteTemplate(template, model);
            Assert.That(result, Is.EqualTo("Hello"));
        }

        [Test]
        public void Should_throw_if_required_override_is_missing()
        {
            var model = new { };
            RegisterTemplate("master", S.Block(S.WriteString("Hello"), S.Override("foo")));
            var template = S.Extend("master");
            Assert.Throws<VeilCompilerException>(() =>
            {
                ExecuteTemplate(template, model);
            });
        }

        [Test]
        public void Should_use_default_for_a_section_if_not_overridden()
        {
            var model = new { };
            RegisterTemplate("master", S.Block(S.WriteString("Hello "), S.Override("foo", S.WriteString("World"))));
            var template = S.Extend("master");
            var result = ExecuteTemplate(template, model);
            Assert.That(result, Is.EqualTo("Hello World"));
        }

        [Test]
        public void Should_be_able_to_nest_extends()
        {
            var model = new { };
            RegisterTemplate("one", S.Block(S.Override("start"), S.Override("middle"), S.Override("end")));
            RegisterTemplate("two", S.Extend("one", new Dictionary<string, SyntaxTreeNode> {
                {"start", S.WriteString("Hello ")},
                {"middle", S.WriteString("there ")},
                {"end", S.Override("name", S.WriteString("world"))}
            }));
            var template = S.Extend("two", new Dictionary<string, SyntaxTreeNode>
            {
                {"start", S.WriteString("Well hello ")},
                {"name", S.WriteString("Bob")}
            });
            var result = ExecuteTemplate(template, model);
            Assert.That(result, Is.EqualTo("Well hello there Bob"));
        }
    }
}