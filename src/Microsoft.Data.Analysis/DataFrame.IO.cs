﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;

namespace Microsoft.Data.Analysis
{
    public partial class DataFrame
    {
        private const int DefaultStreamReaderBufferSize = 1024;

        private static Type GuessKind(int col, List<string[]> read)
        {
            Type res = typeof(string);
            int nbline = 0;
            foreach (var line in read)
            {
                if (col >= line.Length)
                    throw new FormatException(string.Format(Strings.LessColumnsThatExpected, nbline + 1));

                string val = line[col];

                if (string.Equals(val, "null", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool boolParse = bool.TryParse(val, out bool boolResult);
                if (boolParse)
                {
                    res = DetermineType(nbline == 0, typeof(bool), res);
                    ++nbline;
                    continue;
                }
                else
                {
                    if (string.IsNullOrEmpty(val))
                    {
                        res = DetermineType(nbline == 0, typeof(bool), res);
                        continue;
                    }
                }
                bool floatParse = float.TryParse(val, out float floatResult);
                if (floatParse)
                {
                    res = DetermineType(nbline == 0, typeof(float), res);
                    ++nbline;
                    continue;
                }
                else
                {
                    if (string.IsNullOrEmpty(val))
                    {
                        res = DetermineType(nbline == 0, typeof(float), res);
                        continue;
                    }
                }
                res = DetermineType(nbline == 0, typeof(string), res);
                ++nbline;
            }
            return res;
        }

        private static Type DetermineType(bool first, Type suggested, Type previous)
        {
            if (first)
                return suggested;
            else
                return MaxKind(suggested, previous);
        }

        private static Type MaxKind(Type a, Type b)
        {
            if (a == typeof(string) || b == typeof(string))
                return typeof(string);
            if (a == typeof(float) || b == typeof(float))
                return typeof(float);
            if (a == typeof(bool) || b == typeof(bool))
                return typeof(bool);
            return typeof(string);
        }

        /// <summary>
        /// Reads a text file as a DataFrame.
        /// Follows pandas API.
        /// </summary>
        /// <param name="filename">filename</param>
        /// <param name="separator">column separator</param>
        /// <param name="header">has a header or not</param>
        /// <param name="columnNames">column names (can be empty)</param>
        /// <param name="dataTypes">column types (can be empty)</param>
        /// <param name="numRows">number of rows to read</param>
        /// <param name="guessRows">number of rows used to guess types</param>
        /// <param name="addIndexColumn">add one column with the row index</param>
        /// <param name="encoding">The character encoding. Defaults to UTF8 if not specified</param>
        /// <returns>DataFrame</returns>
        public static DataFrame LoadCsv(string filename,
                                char separator = ',', bool header = true,
                                string[] columnNames = null, Type[] dataTypes = null,
                                int numRows = -1, int guessRows = 10,
                                bool addIndexColumn = false, Encoding encoding = null)
        {
            using (Stream fileStream = new FileStream(filename, FileMode.Open))
            {
                return LoadCsv(fileStream,
                                  separator: separator, header: header, columnNames: columnNames, dataTypes: dataTypes, numberOfRowsToRead: numRows,
                                  guessRows: guessRows, addIndexColumn: addIndexColumn, encoding: encoding);
            }
        }

        private static string GetColumnName(string[] columnNames, int columnIndex)
        {
            return columnNames == null ? "Column" + columnIndex.ToString() : columnNames[columnIndex];
        }

        private static DataFrameColumn CreateColumn(Type kind, string[] columnNames, int columnIndex)
        {
            DataFrameColumn ret;
            if (kind == typeof(bool))
            {
                ret = new BooleanDataFrameColumn(GetColumnName(columnNames, columnIndex));
            }
            else if (kind == typeof(int))
            {
                ret = new Int32DataFrameColumn(GetColumnName(columnNames, columnIndex));
            }
            else if (kind == typeof(float))
            {
                ret = new SingleDataFrameColumn(GetColumnName(columnNames, columnIndex));
            }
            else if (kind == typeof(string))
            {
                ret = new StringDataFrameColumn(GetColumnName(columnNames, columnIndex), 0);
            }
            else if (kind == typeof(long))
            {
                ret = new Int64DataFrameColumn(GetColumnName(columnNames, columnIndex));
            }
            else if (kind == typeof(decimal))
            {
                ret = new DecimalDataFrameColumn(GetColumnName(columnNames, columnIndex));
            }
            else if (kind == typeof(byte))
            {
                ret = new ByteDataFrameColumn(GetColumnName(columnNames, columnIndex));
            }
            else if (kind == typeof(char))
            {
                ret = new CharDataFrameColumn(GetColumnName(columnNames, columnIndex));
            }
            else if (kind == typeof(double))
            {
                ret = new DoubleDataFrameColumn(GetColumnName(columnNames, columnIndex));
            }
            else if (kind == typeof(sbyte))
            {
                ret = new SByteDataFrameColumn(GetColumnName(columnNames, columnIndex));
            }
            else if (kind == typeof(short))
            {
                ret = new Int16DataFrameColumn(GetColumnName(columnNames, columnIndex));
            }
            else if (kind == typeof(uint))
            {
                ret = new UInt32DataFrameColumn(GetColumnName(columnNames, columnIndex));
            }
            else if (kind == typeof(ulong))
            {
                ret = new UInt64DataFrameColumn(GetColumnName(columnNames, columnIndex));
            }
            else if (kind == typeof(ushort))
            {
                ret = new UInt16DataFrameColumn(GetColumnName(columnNames, columnIndex));
            }
            else
            {
                throw new NotSupportedException(nameof(kind));
            }
            return ret;
        }

        /// <summary>
        /// Reads a seekable stream of CSV data into a DataFrame.
        /// Follows pandas API.
        /// </summary>
        /// <param name="csvStream">stream of CSV data to be read in</param>
        /// <param name="separator">column separator</param>
        /// <param name="header">has a header or not</param>
        /// <param name="columnNames">column names (can be empty)</param>
        /// <param name="dataTypes">column types (can be empty)</param>
        /// <param name="numberOfRowsToRead">number of rows to read not including the header(if present)</param>
        /// <param name="guessRows">number of rows used to guess types</param>
        /// <param name="addIndexColumn">add one column with the row index</param>
        /// <param name="encoding">The character encoding. Defaults to UTF8 if not specified</param>
        /// <returns><see cref="DataFrame"/></returns>
        public static DataFrame LoadCsv(Stream csvStream,
                                char separator = ',', bool header = true,
                                string[] columnNames = null, Type[] dataTypes = null,
                                long numberOfRowsToRead = -1, int guessRows = 10, bool addIndexColumn = false,
                                Encoding encoding = null)
        {
            if (!csvStream.CanSeek)
            {
                throw new ArgumentException(Strings.NonSeekableStream, nameof(csvStream));
            }

            if (dataTypes == null && guessRows <= 0)
            {
                throw new ArgumentException(string.Format(Strings.ExpectedEitherGuessRowsOrDataTypes, nameof(guessRows), nameof(dataTypes)));
            }

            var linesForGuessType = new List<string[]>();
            long rowline = 0;
            int numberOfColumns = dataTypes?.Length ?? 0;

            if (header == true && numberOfRowsToRead != -1)
            {
                numberOfRowsToRead++;
            }

            List<DataFrameColumn> columns;
            long streamStart = csvStream.Position;
            // First pass: schema and number of rows.
            using (var streamReader = new StreamReader(csvStream, encoding ?? Encoding.UTF8, detectEncodingFromByteOrderMarks: true, DefaultStreamReaderBufferSize, leaveOpen: true))
            {
                string line = null;
                line = streamReader.ReadLine();
                while (line != null)
                {
                    if ((numberOfRowsToRead == -1) || rowline < numberOfRowsToRead)
                    {
                        if (linesForGuessType.Count < guessRows || (header && rowline == 0))
                        {
                            var spl = line.Split(separator);
                            if (header && rowline == 0)
                            {
                                if (columnNames == null)
                                {
                                    columnNames = spl;
                                }
                            }
                            else
                            {
                                linesForGuessType.Add(spl);
                                numberOfColumns = Math.Max(numberOfColumns, spl.Length);
                            }
                        }
                    }
                    ++rowline;
                    if (rowline == guessRows || guessRows == 0)
                    {
                        break;
                    }
                    line = streamReader.ReadLine();
                }

                if (rowline == 0)
                {
                    throw new FormatException(Strings.EmptyFile);
                }

                columns = new List<DataFrameColumn>(numberOfColumns);
                // Guesses types or looks up dataTypes and adds columns.
                for (int i = 0; i < numberOfColumns; ++i)
                {
                    Type kind = dataTypes == null ? GuessKind(i, linesForGuessType) : dataTypes[i];
                    columns.Add(CreateColumn(kind, columnNames, i));
                }

                DataFrame ret = new DataFrame(columns);
                line = null;
                streamReader.DiscardBufferedData();
                streamReader.BaseStream.Seek(streamStart, SeekOrigin.Begin);

                // Fills values.
                line = streamReader.ReadLine();
                rowline = 0;
                while (line != null && (numberOfRowsToRead == -1 || rowline < numberOfRowsToRead))
                {
                    var spl = line.Split(separator);
                    if (header && rowline == 0)
                    {
                        // Skips.
                    }
                    else
                    {
                        ret.Append(spl, inPlace: true);
                    }
                    ++rowline;
                    line = streamReader.ReadLine();
                }

                if (addIndexColumn)
                {
                    PrimitiveDataFrameColumn<int> indexColumn = new PrimitiveDataFrameColumn<int>("IndexColumn", columns[0].Length);
                    for (int i = 0; i < columns[0].Length; i++)
                    {
                        indexColumn[i] = i;
                    }
                    columns.Insert(0, indexColumn);
                }
                return ret;
            }
        }
        
        /// <summary>
        /// Reads an implementation of IDataReader into a DataFrame.
        /// </summary>
        /// <param name="reader">DataReader to be read in</param>
        /// <param name="columnNames">column names (can be empty)</param>
        /// <param name="dataTypes">column types (can be empty)</param>
        /// <param name="numberOfRowsToRead">number of rows to read not including the header(if present)</param>
        /// <param name="addIndexColumn">add one column with the row index</param>
        /// <returns><see cref="DataFrame"/></returns>
        public static DataFrame FromDataReader(IDataReader reader, string[] columnNames = null, Type[] dataTypes = null, long numberOfRowsToRead = -1, bool addIndexColumn = false)
        {
            DataTable schemaTable = reader.GetSchemaTable();
            int numberOfColumns = schemaTable.Rows.Count;

            if (columnNames == null)
            {
                columnNames = new string[numberOfColumns];
                for (int i = 0; i < numberOfColumns; ++i)
                {
                    string columnName = schemaTable.Rows[i]["ColumnName"].ToString();
                    columnNames[i] = string.IsNullOrWhiteSpace(columnName) ? $"Column{i}" : columnName;
                }
            }

            var columns = new List<DataFrameColumn>(numberOfColumns);
            if (dataTypes == null)
            {
                for (int i = 0; i < numberOfColumns; ++i)
                {
                    var kind = (Type)schemaTable.Rows[i]["DataType"];
                    columns.Add(CreateColumn(kind, columnNames, i));
                }
            }
            else
            {
                for (int i = 0; i < numberOfColumns; ++i)
                {
                    columns.Add(CreateColumn(dataTypes[i], columnNames, i));
                }
            }

            long rowline = 0;
            var ret = new DataFrame(columns);
            while (reader.Read() && (numberOfRowsToRead == -1 || rowline < numberOfRowsToRead))
            {
                ret.Append(GetRecordValues(reader), inPlace: true);
                ++rowline;
            }

            if (addIndexColumn)
            {
                PrimitiveDataFrameColumn<int> indexColumn = new PrimitiveDataFrameColumn<int>("IndexColumn", columns[0].Length);
                for (int i = 0; i < columns[0].Length; i++)
                {
                    indexColumn[i] = i;
                }
                columns.Insert(0, indexColumn);
            }
            return ret;

            IEnumerable<object> GetRecordValues(IDataRecord record)
            {
                for (int i = 0; i < record.FieldCount; i++)
                {
                    yield return record[i] == DBNull.Value ? null : record[i];
                }
            }
        }
    }
}
