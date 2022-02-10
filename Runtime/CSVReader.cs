using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace TLP.Sheets
{
    /// <summary>
    /// Reads CSV files.
    /// </summary>
    public static class CSVReader
    {
        #region Read into string arrays

        /// <summary>
        /// Reads CSV data into string arrays. Every line will be an array.
        /// </summary>
        /// <param name="data">CSV data as a string.</param>
        /// <param name="skipHeader">Skips the first line if true (for headers, for example).</param>
        /// <returns>An array of string arrays. Every line will be a new array of fields as strings.</returns>
        public static string[][] Read(string data, bool skipHeader = true)
        {
            using (StringReader sr = new StringReader(data))
            {
                return Read(sr, skipHeader);
            }
        }

        /// <summary>
        /// Reads CSV data into string arrays. Every line will be an array.
        /// </summary>
        /// <param name="str">A stream holding the CSV data.</param>
        /// <param name="skipHeader">Skips the first line if true (for headers, for example).</param>
        /// <returns>An array of string arrays. Every line will be a new array of fields as strings.</returns>
        public static string[][] Read(TextReader str, bool skipHeader = true)
        {
            List<string[]> result = new List<string[]>();

            string line = str.ReadLine();
            if (skipHeader)
                line = str.ReadLine();

            while (line != null)
            {
                result.Add(ParseCSVLine(line));
                line = str.ReadLine();
            }

            return result.ToArray();
        }

        #endregion

        #region Read into classes & structs

        /// <summary>
        /// Reads CSV data and tries to convert every line into an object of type T.
        /// Returns the list of objects.
        /// </summary>
        /// <typeparam name="T">Type into which the data will be converted.</typeparam>
        /// <param name="data">CSV data as a string.</param>
        /// <param name="readFirstLineAsHeader">
        /// If true, the first line will specify the names of the fields. If false, you need to set headerOverride and the first line will be treated just like the others.
        /// </param>
        /// <param name="headerOverride">
        /// Positional names of the object members. This will be used as the default setting even if readFirstLineAsHeader is set, but it can be set to null.
        /// </param>
        /// <param name="fillPrivateMembers">If true, private and protected members will be assigned too.</param>
        /// <param name="ignoreCase">If true, member names will be case-insensitive.</param>
        /// <param name="conversionErrors">Specifies what happens when a conversion error occurs.</param>
        /// <param name="conversionFunction">If set, this function will be called for every value that's about to be filled.</param>
        /// <returns>An array of objects, each filled with data from a line of the CSV.</returns>
        public static T[] Read<T>(string data,
            bool readFirstLineAsHeader = true,
            string[] headerOverride = null,
            bool fillPrivateMembers = false,
            bool ignoreCase = true,
            ConversionErrorHandling conversionErrors = ConversionErrorHandling.SkipItem,
            ConversionFunction conversionFunction = null
            )
            where T: new()
        {
            using (StringReader sr = new StringReader(data))
            {
                return Read<T>(sr, readFirstLineAsHeader, headerOverride, fillPrivateMembers, ignoreCase, conversionErrors, conversionFunction);
            }
        }

        /// <summary>
        /// Reads CSV data and tries to convert every line into an object of type T.
        /// Returns the list of objects.
        /// </summary>
        /// <typeparam name="T">Type into which the data will be converted.</typeparam>
        /// <param name="str">A stream of CSV data.</param>
        /// <param name="readFirstLineAsHeader">
        /// If true, the first line will specify the names of the fields. If false, you need to set headerOverride and the first line will be treated just like the others.
        /// </param>
        /// <param name="headerOverride">
        /// Positional names of the object members. This will be used as the default setting even if readFirstLineAsHeader is set, but it can be set to null.
        /// </param>
        /// <param name="fillPrivateMembers">If true, private and protected members will be assigned too.</param>
        /// <param name="ignoreCase">If true, member names will be case-insensitive.</param>
        /// <param name="conversionErrors">Specifies what happens when a conversion error occurs.</param>
        /// <param name="conversionFunction">If set, this function will be called for every value that's about to be filled.</param>
        /// <returns>An array of objects, each filled with data from a line of the CSV.</returns>
        public static T[] Read<T>(TextReader str,
            bool readFirstLineAsHeader = true,
            string[] headerOverride = null,
            bool fillPrivateMembers = false,
            bool ignoreCase = true,
            ConversionErrorHandling conversionErrors = ConversionErrorHandling.SkipItem,
            ConversionFunction conversionFunction = null
            )
            where T : new()
        {
            if (!readFirstLineAsHeader && (headerOverride == null))
                throw new System.InvalidOperationException("Either readFirstLineAsHeader or headerOverride must be set!");

            List<T> result = new List<T>();

            // Read first line; use it as a header if requested
            string line = str.ReadLine();
            if (readFirstLineAsHeader)
            {
                if (headerOverride == null)
                    headerOverride = ParseCSVLine(line);

                line = str.ReadLine();
            }

            // Get reflection info for this type now that we have the member names. We only care about fields and properties.
            FieldInfo[] fields = new FieldInfo[headerOverride.Length];
            PropertyInfo[] properties = new PropertyInfo[headerOverride.Length];

            // Some options here
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public;
            if (fillPrivateMembers)
                bindingFlags |= BindingFlags.NonPublic;
            if (ignoreCase)
                bindingFlags |= BindingFlags.IgnoreCase;

            for (int i = 0; i < headerOverride.Length; i++)
            {
                if (!string.IsNullOrEmpty(headerOverride[i]))
                {
                    var field = typeof(T).GetField(headerOverride[i], bindingFlags);
                    if (field != null)
                        fields[i] = field;

                    var property = typeof(T).GetProperty(headerOverride[i], bindingFlags);
                    if (property != null)
                        properties[i] = property;
                }
            }

            // Start reading line by line; obj will be inserted into the array unless errorOccured gets set.
            while (line != null)
            {
                var elems = ParseCSVLine(line);

                //T obj = default(T);
                T obj = new T();

                bool errorOccured = false;

                for (int i = 0; i < elems.Length; i++)
                {
                    if ((i < fields.Length) && (fields[i] != null))
                    {
                        try
                        {
                            // Note: if the object is a struct, then fields.SetValue would get a copy of it and set the field for the copy.
                            // To avoid this, I box the struct as an object.
                            object boxed = obj;
                            object convertedValue = null;

                            bool customConverted = false;

                            if (conversionFunction != null)
                                customConverted = conversionFunction(headerOverride[i], elems[i], ref convertedValue);

                            if (!customConverted)
                                convertedValue = System.Convert.ChangeType(elems[i], fields[i].FieldType);
                            
                            fields[i].SetValue(obj, convertedValue);

                            obj = (T)boxed;
                        }
                        catch
                        {
                            if (conversionErrors == ConversionErrorHandling.ThrowException)
                                throw;
                            else if (conversionErrors == ConversionErrorHandling.SkipItem)
                            {
                                errorOccured = true;
                                break;
                            }
                        }
                    }
                    if ((i < properties.Length) && (properties[i] != null))
                    {
                        try
                        {
                            // Note: same boxing method as above
                            object boxed = obj;
                            object convertedValue = null;

                            bool customConverted = false;

                            if (conversionFunction != null)
                                customConverted = conversionFunction(headerOverride[i], elems[i], ref convertedValue);

                            if (!customConverted)
                                convertedValue = System.Convert.ChangeType(elems[i], properties[i].PropertyType);

                            properties[i].SetValue(obj, convertedValue);

                            obj = (T)boxed;
                        }
                        catch
                        {
                            if (conversionErrors == ConversionErrorHandling.ThrowException)
                            {
                                throw;
                            }
                            else if (conversionErrors == ConversionErrorHandling.SkipItem)
                            {
                                errorOccured = true;
                                break;
                            }
                        }
                    }
                }

                if (!errorOccured)
                    result.Add(obj);

                line = str.ReadLine();
            }

            return result.ToArray();
        }

        #endregion

        /// <summary>
        /// Parses a comma-separated line. Apostrophed fields are handled too.
        /// It is fairly error-resilient; unclosed apostrophes will 
        /// </summary>
        /// <param name="line">A single line of comma-separated data.</param>
        /// <returns>An array of values as strings.</returns>
        public static string[] ParseCSVLine(string line)
        {
            List<string> elements = new List<string>();

            int start = 0;
            bool inApostrophe = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == ',')
                {
                    if (!inApostrophe)
                    {
                        // Starts and ends with apostrophe?
                        // Remove first and last char, replace "" with "
                        if ((line[start] == '"') && (line[i - 1] == '"'))
                        {
                            string str = line.Substring(start + 1, i - start - 2);
                            str = str.Replace("\"\"", "\"");
                            elements.Add(str);
                        }
                        // Simple add otherwise
                        else
                        {
                            elements.Add(line.Substring(start, i - start));
                        }

                        start = i + 1;
                    }
                }
                else if (line[i] == '"')
                {
                    inApostrophe = !inApostrophe;
                }
            }

            // Add the last element. Also check start/end apostrophe
            if ((start < line.Length) && (line[start] == '"') && (line[line.Length - 1] == '"'))
            {
                string str = line.Substring(start + 1, line.Length - start - 2);
                str = str.Replace("\"\"", "\"");
                elements.Add(str);
            }
            // Simple add otherwise
            else
            {
                elements.Add(line.Substring(start, line.Length - start));
            }

            return elements.ToArray();
        }

        /// <summary>
        /// Conversion error behaviour.
        /// </summary>
        public enum ConversionErrorHandling
        {
            /// <summary>Throw an exception when a conversion error occurs. This will stop the whole parsing.</summary>
            ThrowException,
            /// <summary>Skip the entire item when a conversion error occurs.</summary>
            SkipItem,
            /// <summary>Ignore conversion errors; faulty fields will be unchanged.</summary>
            SkipMember
        }

        /// <summary>
        /// Custom conversion callback.
        /// </summary>
        /// <param name="fieldName">Name of the field to convert.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="result">The result of the conversion, if successful.</param>
        /// <returns>Return true to use the result, false to ignore the function and use the default conversion.</returns>
        public delegate bool ConversionFunction(string fieldName, string value, ref object result);
    }
}