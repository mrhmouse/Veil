﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Sigil;
using Veil.Parser;

namespace Veil.Compiler
{
    internal partial class VeilTemplateCompiler<T>
    {
        private LinkedList<Action<Emit<Action<TextWriter, T>>>> scopeStack;
        private readonly Emit<Action<TextWriter, T>> emitter;
        private readonly Func<string, Type, SyntaxTreeNode> includeParser;

        public VeilTemplateCompiler(Func<string, Type, SyntaxTreeNode> includeParser)
        {
            scopeStack = new LinkedList<Action<Emit<Action<TextWriter, T>>>>();
            emitter = Emit<Action<TextWriter, T>>.NewDynamicMethod();
            this.includeParser = includeParser;
        }

        public Action<TextWriter, T> Compile(SyntaxTreeNode templateSyntaxTree)
        {
            AddModelScope(e => e.LoadArgument(1));
            EmitNode(templateSyntaxTree);

            emitter.Return();
            return emitter.CreateDelegate();
        }

        private IDisposable CreateLocalScopeStack()
        {
            var oldScopeStack = scopeStack;
            scopeStack = new LinkedList<Action<Emit<Action<TextWriter, T>>>>();
            return new ActionDisposable(() =>
            {
                scopeStack = oldScopeStack;
            });
        }

        private void AddModelScope(Action<Emit<Action<TextWriter, T>>> scope)
        {
            scopeStack.AddFirst(scope);
        }

        private void RemoveModelScope()
        {
            scopeStack.RemoveFirst();
        }

        private void PushCurrentModelOnStack()
        {
            scopeStack.First.Value.Invoke(emitter);
        }

        private void EvaluateExpression(SyntaxTreeNode.ExpressionNode expression)
        {
            switch (expression.Scope)
            {
                case SyntaxTreeNode.ExpressionScope.CurrentModelOnStack:
                    scopeStack.First.Value.Invoke(emitter);
                    break;

                case SyntaxTreeNode.ExpressionScope.RootModel:
                    scopeStack.Last.Value.Invoke(emitter);
                    break;

                default:
                    throw new VeilCompilerException("Uknown expression scope '{0}'".FormatInvariant(expression.Scope));
            }
            EvaluateExpressionAgainstModelOnStack(expression);
        }

        private void EvaluateExpressionAgainstModelOnStack(SyntaxTreeNode.ExpressionNode expression)
        {
            if (expression is SyntaxTreeNode.ExpressionNode.PropertyExpressionNode)
            {
                emitter.CallMethod(((SyntaxTreeNode.ExpressionNode.PropertyExpressionNode)expression).PropertyInfo.GetGetMethod());
            }
            else if (expression is SyntaxTreeNode.ExpressionNode.FieldExpressionNode)
            {
                emitter.LoadField(((SyntaxTreeNode.ExpressionNode.FieldExpressionNode)expression).FieldInfo);
            }
            else if (expression is SyntaxTreeNode.ExpressionNode.SubModelExpressionNode)
            {
                EvaluateExpressionAgainstModelOnStack(((SyntaxTreeNode.ExpressionNode.SubModelExpressionNode)expression).ModelExpression);
                EvaluateExpressionAgainstModelOnStack(((SyntaxTreeNode.ExpressionNode.SubModelExpressionNode)expression).SubModelExpression);
            }
            else if (expression is SyntaxTreeNode.ExpressionNode.FunctionCallExpressionNode)
            {
                emitter.CallMethod(((SyntaxTreeNode.ExpressionNode.FunctionCallExpressionNode)expression).MethodInfo);
            }
            else if (expression is SyntaxTreeNode.ExpressionNode.CollectionHasItemsNode)
            {
                var hasItems = (SyntaxTreeNode.ExpressionNode.CollectionHasItemsNode)expression;
                var count = typeof(ICollection).GetProperty("Count");
                EvaluateExpressionAgainstModelOnStack(hasItems.CollectionExpression);
                emitter.CallMethod(count.GetGetMethod());
                emitter.LoadConstant(0);
                emitter.CompareEqual();
                emitter.LoadConstant(0);
                emitter.CompareEqual();
            }
            else if (expression is SyntaxTreeNode.ExpressionNode.SelfExpressionNode)
            {
            }
            else
            {
                throw new VeilCompilerException("Unknown expression type '{0}'".FormatInvariant(expression.GetType().Name));
            }
        }

        private void LoadWriterToStack()
        {
            emitter.LoadArgument(0);
        }

        private void CallWriteFor(Type typeOfItemOnStack)
        {
            if (!writers.ContainsKey(typeOfItemOnStack)) throw new VeilCompilerException("Unable to call TextWriter.Write() for item of type '{0}'".FormatInvariant(typeOfItemOnStack.Name));
            emitter.CallMethod(writers[typeOfItemOnStack]);
        }

        private static readonly IDictionary<Type, MethodInfo> writers = new Dictionary<Type, MethodInfo>
        {
            { typeof(string), typeof(TextWriter).GetMethod("Write", new[] { typeof(string) }) },
            { typeof(int), typeof(TextWriter).GetMethod("Write", new[] { typeof(int) }) },
            { typeof(double), typeof(TextWriter).GetMethod("Write", new[] { typeof(double) }) },
            { typeof(float), typeof(TextWriter).GetMethod("Write", new[] { typeof(float) }) },
            { typeof(long), typeof(TextWriter).GetMethod("Write", new[] { typeof(long) }) },
            { typeof(uint), typeof(TextWriter).GetMethod("Write", new[] { typeof(uint) }) },
            { typeof(ulong), typeof(TextWriter).GetMethod("Write", new[] { typeof(ulong) }) },
        };
    }
}