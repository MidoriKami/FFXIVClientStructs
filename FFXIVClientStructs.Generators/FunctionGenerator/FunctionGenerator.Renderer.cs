﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FFXIVClientStructs.Generators.FunctionGenerator;

public sealed partial class FunctionGenerator
{
    internal sealed class Renderer
    {
        private static readonly string GeneratedCodeAttribute =
            $"global::System.CodeDom.Compiler.GeneratedCodeAttribute(" +
            $"\"{typeof(Renderer).Assembly.GetName().Name}\", " +
            $"\"{typeof(Renderer).Assembly.GetName().Version}\")";

        private readonly IndentedStringBuilder _builder = new IndentedStringBuilder();

        public string Render(TargetStruct targetStruct, CancellationToken cancellationToken)
        {
            _builder.Clear();
            _builder.AppendLine("// <auto-generated/>");

            if (!string.IsNullOrWhiteSpace(targetStruct.Namespace))
            {
                _builder.AppendLine();
                _builder.AppendLine($"namespace {targetStruct.Namespace};");
            }

            _builder.AppendLine();
            
            TargetStruct?  parent = targetStruct.ParentStruct;
            var parentStructs = new List<string>();

            while (parent != null)
            {
                parentStructs.Add($"unsafe partial {parent.Keyword} {parent.Name}");
                parent = parent.ParentStruct;
            }
            
            for (int i = parentStructs.Count - 1; i >= 0; i--)
            {
                _builder.AppendLine($"{parentStructs[i]}");
                _builder.AppendLine("{");
                _builder.Indent();
            }

            _builder.AppendLine($"[{GeneratedCodeAttribute}]");
            _builder.AppendLine($"unsafe partial {targetStruct.Keyword} {targetStruct.Name}");
            _builder.AppendLine("{");
            _builder.Indent();

            if (targetStruct.Functions.Any(f => f.Signature is not null))
                RenderFunctionPointers(targetStruct.Functions, targetStruct.Name);
            if (targetStruct.Functions.Any(f => f.VirtualIndex is not null))
                RenderVirtualTable(targetStruct.Functions, targetStruct.Name);
            
            foreach (Function f in targetStruct.Functions)
            {
                _builder.AppendLine();
                cancellationToken.ThrowIfCancellationRequested();
                if (f.Signature is not null)
                    RenderMemberFunction(f, targetStruct.Name);
            }

            _builder.DecrementIndent();
            _builder.AppendLine("}");

            parent = targetStruct.ParentStruct;
            while (parent != null)
            {
                _builder.DecrementIndent();
                _builder.AppendLine("}");
                parent = parent.ParentStruct;
            }


            return _builder.ToString();
        }

        private void RenderFunctionPointers(IEnumerable<Function> functions, string structName)
        {
            _builder.AppendLine("public unsafe static class FunctionPointers");
            _builder.AppendLine("{");
            _builder.Indent();
            foreach (Function f in functions)
            {
                if (f.Signature is null)
                    continue;

                string parameterTypes =
                    f.Parameters.Any() ? string.Join(", ", f.Parameters.Select(p => p.Type)) + ", " : "";
                string thisPtrType = f.IsStatic ? "" : structName + "*, ";
                
                _builder.AppendLine(
                    $"public static delegate* unmanaged[Stdcall] <{thisPtrType}{parameterTypes}{f.ReturnType}> {f.Name} {{ internal set; get; }}");
            }

            _builder.DecrementIndent();
            _builder.AppendLine("}");
        }

        private void RenderVirtualTable(IEnumerable<Function> functions, string structName)
        {
            
        }
        
        private void RenderMemberFunction(Function f, string structName)
        {
            string paramNamesAndTypes = String.Join(", ", f.Parameters.Select(p => $"{p.Type} {p.Name}"));
            string paramNames = String.Join(", ", f.Parameters.Select(p => p.Name));
            string returnString = f.ReturnType == "void" ? "" : "return ";

            _builder.AppendLine($"{f.Modifiers} {f.ReturnType} {f.Name}({paramNamesAndTypes})");
            _builder.AppendLine("{");
            _builder.Indent();
            _builder.AppendLine($"if (FunctionPointers.{f.Name} is null)");
            _builder.Indent();
            _builder.AppendLine($"throw new InvalidOperationException(\"Function pointer for {structName}.{f.Name} is null. Did you forget to call Resolver.Initialize?\");");
            _builder.DecrementIndent();
            _builder.AppendLine();
            if (f.IsStatic)
            {
                _builder.AppendLine($"{returnString}FunctionPointers.{f.Name}({paramNames});");
            }
            else
            {
                _builder.AppendLine($"fixed({structName}* thisPtr = &this)");
                _builder.AppendLine("{");
                _builder.Indent();
                if (f.Parameters.Any())
                    paramNames = ", " + paramNames;
                _builder.AppendLine($"{returnString}FunctionPointers.{f.Name}(thisPtr{paramNames});");
                _builder.DecrementIndent();
                _builder.AppendLine("}");
            }
            _builder.DecrementIndent();
            _builder.AppendLine("}");
        }
    }
    
}