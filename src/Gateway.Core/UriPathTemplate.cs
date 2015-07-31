// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Text;

    public class UriPathTemplate
    {
        const char PathSeparator = '/';
        const char VariableNameValueSeparator = '=';
        const char WildcardCharacter = '*';
        const char VariablePlaceholderStartCharacter = '{';
        const char VariablePlaceholderEndCharacter = '}';
        const int EstimatedVariableValueLength = 20;

        public static readonly char[] PathSegmentTerminationCharacters = { PathSeparator };

        TemplatePart[] parts;
        int projectedLength;

        public UriPathTemplate(string template)
        {
            Contract.Requires(template != null);

            this.Compile(template);
        }

        public static bool TryParse(string value, out UriPathTemplate uriPathTemplate)
        {
            try
            {
                uriPathTemplate = new UriPathTemplate(value);
                return true;
            }
            catch (Exception ex)
            {
                uriPathTemplate = null;
                return false;
            }
        }

        public string Bind(IDictionary<string, string> variables)
        {
            var result = new StringBuilder(this.projectedLength); // todo: calc estimated initial capacity during compilation

            int index = 0;
            TemplatePart[] templateParts = this.parts;
            int partsLength = templateParts.Length;

            for (; index < partsLength; index++)
            {
                string partValue = templateParts[index].Bind(variables);
                if (string.IsNullOrEmpty(partValue))
                {
                    continue;
                }
                if (result.Length > 0 && result[result.Length - 1] == PathSeparator && partValue[0] == PathSeparator)
                {
                    result.Append(partValue, 1, partValue.Length - 1);
                }
                else
                {
                    result.Append(partValue);
                }
            }
            return result.ToString();
        }

        void Compile(string template)
        {
            var templateParts = new List<TemplatePart>();
            int initialCapacity = 0;

            int length = template.Length;
            int index = 0;

            while (index < length)
            {
                int varStartIndex = template.IndexOf(VariablePlaceholderStartCharacter, index);
                if (varStartIndex != -1)
                {
                    int varEndIndex = template.IndexOf(VariablePlaceholderEndCharacter, varStartIndex + 1);
                    if (varEndIndex == -1)
                    {
                        throw new Exception("Variable definition is never closed.");
                    }

                    string varDefinition = template.Substring(varStartIndex + 1, varEndIndex - varStartIndex - 1);

                    if (varDefinition.IndexOf(VariablePlaceholderStartCharacter) != -1)
                    {
                        throw new Exception("Variable definition syntax is invalid in template definition.");
                    }

                    int eqIndex = varDefinition.IndexOf(VariableNameValueSeparator);
                    int nameOffset;
                    if (varDefinition[0] == WildcardCharacter)
                    {
                        if (varEndIndex < length - 1)
                        {
                            throw new InvalidOperationException("Wildcard variable can only be used at the end of the template.");
                        }
                        nameOffset = 1;
                    }
                    else
                    {
                        nameOffset = 0;
                    }
                    string varName;
                    if (eqIndex == -1)
                    {
                        varName = nameOffset == 0 ? varDefinition : varDefinition.Substring(nameOffset);
                    }
                    else
                    {
                        varName = varDefinition.Substring(nameOffset, eqIndex);
                    }
                    string varDefaultValue = eqIndex == -1 ? null : varDefinition.Substring(eqIndex + 1);

                    if (varStartIndex > index)
                    {
                        int partLength = varStartIndex - index;
                        templateParts.Add(new TemplatePart(template.Substring(index, partLength)));
                        initialCapacity += partLength;
                    }
                    templateParts.Add(new TemplatePart(varName, varDefaultValue));
                    initialCapacity += EstimatedVariableValueLength;
                    index = varEndIndex + 1;
                }
                else
                {
                    int partLength = length - index;
                    templateParts.Add(new TemplatePart(template.Substring(index, partLength)));
                    initialCapacity += partLength;
                    index = length;
                }
            }

            this.parts = templateParts.ToArray();
            this.projectedLength = initialCapacity;
        }

        struct TemplatePart
        {
            readonly string variableName;
            readonly string value;

            public TemplatePart(string value)
            {
                Contract.Requires(value != null);

                this.variableName = null;
                this.value = value;
            }

            public TemplatePart(string variableName, string defaultValue)
            {
                Contract.Requires(variableName != null);

                this.variableName = variableName;
                this.value = defaultValue;
            }

            public string Bind(IDictionary<string, string> variables)
            {
                if (this.variableName == null)
                {
                    return this.value;
                }
                else
                {
                    string variableValue;
                    if (!variables.TryGetValue(this.variableName, out variableValue))
                    {
                        if (this.value == null) // comparison to null is correct. empty string is allowed as a default value.
                        {
                            throw new InvalidOperationException("Variable was not provided and has no default value to fallback to.");
                        }
                        return this.value;
                    }
                    return variableValue;
                }
            }
        }
    }
}