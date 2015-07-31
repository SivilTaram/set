using System;
using System.Collections.Generic;
using System.Text;
using ExcelToolKit.OpenXmlFormat;
using System.IO;
using ExcelToolKit;
using System.Data;
using System.Xml;
using System.Globalization;

namespace ExcelToolKit
{
	public class ExcelOpenXmlReader : IExcelDataReader
	{
		#region Members
		private XlsxWorkbook m_workbook;
		private bool m_isValid;
		private bool m_isClosed;
		private bool m_isFirstRead;
		private string m_exceptionMessage;
		private int m_depth;
		private int m_resultIndex;
		private int m_emptyRowCount;
		private ZipWorker m_zipWorker;
		private XmlReader m_xmlReader;
		private Stream m_sheetStream;
		private object[] m_cellsValues;
		private object[] m_savedCellsValues;

		private bool disposed;
		private bool m_isFirstRowAsColumnNames;
		private const string COLUMN = "Column";
		private string m_instanceId = Guid.NewGuid().ToString();

		private List<int> m_defaultDateTimeStyles;
		private string m_namespaceUri;
		#endregion

		internal ExcelOpenXmlReader()
		{
			m_isValid = true;
			m_isFirstRead = true;

			m_defaultDateTimeStyles = new List<int>(new int[] 
			{
				14, 15, 16, 17, 18, 19, 20, 21, 22, 45, 46, 47
			});

		}

		private void ReadGlobals()
		{
			m_workbook = new XlsxWorkbook(
				m_zipWorker.GetWorkbookStream(),
				m_zipWorker.GetWorkbookRelsStream(),
				m_zipWorker.GetSharedStringsStream(),
				m_zipWorker.GetStylesStream());

			CheckDateTimeNumFmts(m_workbook.Styles.NumFmts);
		}

		private void CheckDateTimeNumFmts(List<XlsxNumFmt> list)
		{
			if (list.Count == 0) return;

			foreach (XlsxNumFmt numFmt in list)
			{
				if (string.IsNullOrEmpty(numFmt.FormatCode)) continue;
				string fc = numFmt.FormatCode.ToLower();

				int pos;
				while ((pos = fc.IndexOf('"')) > 0)
				{
					int endPos = fc.IndexOf('"', pos + 1);

					if (endPos > 0) fc = fc.Remove(pos, endPos - pos + 1);
				}

				//it should only detect it as a date if it contains
				//dd mm mmm yy yyyy
				//h hh ss
				//AM PM
				//and only if these appear as "words" so either contained in [ ]
				//or delimted in someway
				//updated to not detect as date if format contains a #
				var formatReader = new FormatReader() {FormatString = fc};
				if (formatReader.IsDateFormatString())
				{
					m_defaultDateTimeStyles.Add(numFmt.Id);
				}
			}
		}

		private void ReadSheetGlobals(XlsxWorksheet sheet)
		{
			if (m_xmlReader != null) m_xmlReader.Close();
			if (m_sheetStream != null) m_sheetStream.Close();

			m_sheetStream = m_zipWorker.GetWorksheetStream(sheet.Path);

			if (null == m_sheetStream) return;

			m_xmlReader = XmlReader.Create(m_sheetStream);

			//count rows and cols in case there is no dimension elements
			int rows = 0;
			int cols = 0;

			m_namespaceUri = null;
		    int biggestColumn = 0; //used when no col elements and no dimension
			while (m_xmlReader.Read())
			{
				if (m_xmlReader.NodeType == XmlNodeType.Element && m_xmlReader.LocalName == XlsxWorksheet.N_worksheet)
				{
					//grab the namespaceuri from the worksheet element
					m_namespaceUri = m_xmlReader.NamespaceURI;
				}
				
				if (m_xmlReader.NodeType == XmlNodeType.Element && m_xmlReader.LocalName == XlsxWorksheet.N_dimension)
				{
					string dimValue = m_xmlReader.GetAttribute(XlsxWorksheet.A_ref);

					sheet.Dimension = new XlsxDimension(dimValue);
					break;
				}

                //removed: Do not use col to work out number of columns as this is really for defining formatting, so may not contain all columns
                //if (_xmlReader.NodeType == XmlNodeType.Element && _xmlReader.LocalName == XlsxWorksheet.N_col)
                //    cols++;

				if (m_xmlReader.NodeType == XmlNodeType.Element && m_xmlReader.LocalName == XlsxWorksheet.N_row)
                    rows++;

                //check cells so we can find size of sheet if can't work it out from dimension or col elements (dimension should have been set before the cells if it was available)
                //ditto for cols
                if (sheet.Dimension == null && cols == 0 && m_xmlReader.NodeType == XmlNodeType.Element && m_xmlReader.LocalName == XlsxWorksheet.N_c)
                {
                    var refAttribute = m_xmlReader.GetAttribute(XlsxWorksheet.A_r);

                    if (refAttribute != null)
                    {
                        var thisRef = refAttribute.ReferenceToColumnAndRow();
                        if (thisRef[1] > biggestColumn)
                            biggestColumn = thisRef[1];
                    }
                }
					
			}

			//if we didn't get a dimension element then use the calculated rows/cols to create it
			if (sheet.Dimension == null)
			{
                if (cols == 0)
                    cols = biggestColumn;

				if (rows == 0 || cols == 0) 
				{
					sheet.IsEmpty = true;
					return;
				}

				sheet.Dimension = new XlsxDimension(rows, cols);
				
				//we need to reset our position to sheet data
				m_xmlReader.Close();
				m_sheetStream.Close();
				m_sheetStream = m_zipWorker.GetWorksheetStream(sheet.Path);
				m_xmlReader = XmlReader.Create(m_sheetStream);

			}

			//read up to the sheetData element. if this element is empty then there aren't any rows and we need to null out dimension

			m_xmlReader.ReadToFollowing(XlsxWorksheet.N_sheetData, m_namespaceUri);
			if (m_xmlReader.IsEmptyElement)
			{
				sheet.IsEmpty = true;
			}
		}

		private bool ReadSheetRow(XlsxWorksheet sheet)
		{
            if (sheet.ColumnsCount < 0)
            {
                //Console.WriteLine("Columons Count Can NOT BE Negative");
                return false;
            }
			if (null == m_xmlReader) return false;

			if (m_emptyRowCount != 0)
			{
				m_cellsValues = new object[sheet.ColumnsCount];
				m_emptyRowCount--;
				m_depth++;

				return true;
			}

			if (m_savedCellsValues != null)
			{
				m_cellsValues = m_savedCellsValues;
				m_savedCellsValues = null;
				m_depth++;

				return true;
			}

            bool isRow = false;
            bool isSheetData = (m_xmlReader.NodeType == XmlNodeType.Element && m_xmlReader.LocalName == XlsxWorksheet.N_sheetData);
            if(isSheetData)
            {
                isRow = m_xmlReader.ReadToFollowing(XlsxWorksheet.N_row, m_namespaceUri);
            }
            else
            {
                if (m_xmlReader.LocalName == XlsxWorksheet.N_row && m_xmlReader.NodeType == XmlNodeType.EndElement)
                {
                    m_xmlReader.Read();
                }
                isRow = (m_xmlReader.NodeType == XmlNodeType.Element && m_xmlReader.LocalName == XlsxWorksheet.N_row);
                //Console.WriteLine("isRow:{0}/{1}/{2}", isRow,m_xmlReader.NodeType, m_xmlReader.LocalName);
            }

            if (isRow)
            {
                m_cellsValues = new object[sheet.ColumnsCount];

                int rowIndex = int.Parse(m_xmlReader.GetAttribute(XlsxWorksheet.A_r));
                if (rowIndex != (m_depth + 1))
                {
                    m_emptyRowCount = rowIndex - m_depth - 1;
                }

                bool hasValue = false;
                string a_s = String.Empty;
                string a_t = String.Empty;
                string a_r = String.Empty;
                int col = 0;
                int row = 0;

                while (m_xmlReader.Read())
                {
                    if (m_xmlReader.Depth == 2)
                    {
                        break;
                    }

                    if (m_xmlReader.NodeType == XmlNodeType.Element)
                    {
                        hasValue = false;

                        if (m_xmlReader.LocalName == XlsxWorksheet.N_c)
                        {
                            a_s = m_xmlReader.GetAttribute(XlsxWorksheet.A_s);
                            a_t = m_xmlReader.GetAttribute(XlsxWorksheet.A_t);
                            a_r = m_xmlReader.GetAttribute(XlsxWorksheet.A_r);
                            XlsxDimension.XlsxDim(a_r, out col, out row);
                        }
                        else if (m_xmlReader.LocalName == XlsxWorksheet.N_v || m_xmlReader.LocalName == XlsxWorksheet.N_t)
                        {
                            hasValue = true;
                        }
                        else
                        {
                            //Console.WriteLine("Error");
                        }
                    }

                    if (m_xmlReader.NodeType == XmlNodeType.Text  && hasValue)
                    {
                        double number;
                        object o = m_xmlReader.Value;

                        var style = NumberStyles.Any;
                        var culture = CultureInfo.InvariantCulture;

                        if (double.TryParse(o.ToString(), style, culture, out number))
                            o = number;

                        #region Read Cell Value
                        if (null != a_t && a_t == XlsxWorksheet.A_s) //if string
                        {
                            o = m_workbook.SST[int.Parse(o.ToString())].ConvertEscapeChars();
                        } // Requested change 4: missing (it appears that if should be else if)
                        else if (null != a_t && a_t == XlsxWorksheet.N_inlineStr) //if string inline
                        {
                            o = o.ToString().ConvertEscapeChars();
                        }
                        else if (a_t == "b") //boolean
                        {
                            o = m_xmlReader.Value == "1";
                        }
                        else if (null != a_s) //if something else
                        {
                            XlsxXf xf = m_workbook.Styles.CellXfs[int.Parse(a_s)];
                            if (xf.ApplyNumberFormat && o != null && o.ToString() != string.Empty && IsDateTimeStyle(xf.NumFmtId))
                            {
                                o = number.ConvertFromOATime();
                            }
                            else if (xf.NumFmtId == 49)
                            {
                                o = o.ToString();
                            }
                        }
                        #endregion

                        if (col - 1 < m_cellsValues.Length)
                        {
                            //Console.WriteLine(o);
                            if (string.IsNullOrEmpty(o.ToString()))
                            {
                                //Console.WriteLine("Error");
                            }
                            m_cellsValues[col - 1] = o;
                        }
                        else
                        {
                            //Console.WriteLine("Error");
                        }
                    }
                    else
                    {
                        if (m_xmlReader.LocalName == XlsxWorksheet.N_v)
                        {
                            //Console.WriteLine("No Value");
                        }
                    }
                }

                if (m_emptyRowCount > 0)
                {
                    m_savedCellsValues = m_cellsValues;
                    return ReadSheetRow(sheet);
                }
                m_depth++;

                return true;
            }
            else
            {
                //Console.WriteLine(m_xmlReader.LocalName.ToString());
                return false;
            }
		}

        private bool ReadHyperLinks(XlsxWorksheet sheet, DataTable table)
        {
            // ReadTo HyperLinks Node
            if (m_xmlReader == null)
            {
                //Console.WriteLine("m_xmlReader is null");
                return false;
            }

            //Console.WriteLine(m_xmlReader.Depth.ToString());
            
            m_xmlReader.ReadToFollowing(XlsxWorksheet.N_hyperlinks);
            if (m_xmlReader.IsEmptyElement)
            {
                //Console.WriteLine("not find hyperlink");
                return false;
            }

            // Read All HyperLink Node
            while (m_xmlReader.Read())
            {
                if (m_xmlReader.NodeType != XmlNodeType.Element) break;
                if (m_xmlReader.LocalName != XlsxWorksheet.N_hyperlink) break;
                string aref = m_xmlReader.GetAttribute(XlsxWorksheet.A_ref);
                string display = m_xmlReader.GetAttribute(XlsxWorksheet.A_display);
                ////Console.WriteLine("{0}:{1}", aref.Substring(1), display);

                int col = -1;
                int row = -1;
                XlsxDimension.XlsxDim(aref, out col,out row);
                //Console.WriteLine("{0}:[{1},{2}]",aref, row, col);
                if (col >=1 && row >=1)
                {
                    row = row - 1;
                    col = col - 1;
                    if (row==0 &&m_isFirstRowAsColumnNames)
                    {
                        // TODO(fanfeilong):
                        var value = table.Columns[col].ColumnName;
                        XlsCell cell = new XlsCell(value);
                        cell.SetHyperLink(display);
                        table.Columns[col].DefaultValue = cell;
                    }
                    else
                    {
                        var value = table.Rows[row][col];
                        var cell = new XlsCell(value);
                        cell.SetHyperLink(display);
                        //Console.WriteLine(cell.MarkDownText);
                        table.Rows[row][col] = cell;
                    }
                }
            }

            // Close
            m_xmlReader.Close();
            if (m_sheetStream != null)
            {
                m_sheetStream.Close();
            }

            return true;
        }

		private bool InitializeSheetRead()
		{
			if (ResultsCount <= 0) return false;

			ReadSheetGlobals(m_workbook.Sheets[m_resultIndex]);

			if (m_workbook.Sheets[m_resultIndex].Dimension == null) return false;

			m_isFirstRead = false;

			m_depth = 0;
			m_emptyRowCount = 0;

			return true;
		}

		private bool IsDateTimeStyle(int styleId)
		{
			return m_defaultDateTimeStyles.Contains(styleId);
		}


		#region IExcelDataReader Members

		public void Initialize(System.IO.Stream fileStream)
		{
			m_zipWorker = new ZipWorker();
			m_zipWorker.Extract(fileStream);

			if (!m_zipWorker.IsValid)
			{
				m_isValid = false;
				m_exceptionMessage = m_zipWorker.ExceptionMessage;

				Close();

				return;
			}

			ReadGlobals();
		}

		public System.Data.DataSet AsDataSet()
		{
			return AsDataSet(true);
		}

		public System.Data.DataSet AsDataSet(bool convertOADateTime)
		{
			if (!m_isValid) return null;

			DataSet dataset = new DataSet();

			for (int sheetIndex = 0; sheetIndex < m_workbook.Sheets.Count; sheetIndex++)
			{
				DataTable table = new DataTable(m_workbook.Sheets[sheetIndex].Name);

				ReadSheetGlobals(m_workbook.Sheets[sheetIndex]);

				if (m_workbook.Sheets[sheetIndex].Dimension == null) continue;

				m_depth = 0;
				m_emptyRowCount = 0;

				// Reada Columns
                //Console.WriteLine("Read Columns");
				if (!m_isFirstRowAsColumnNames)
				{
                    // No Sheet Columns
					for (int i = 0; i < m_workbook.Sheets[sheetIndex].ColumnsCount; i++)
					{
                        table.Columns.Add(null, typeof(Object));
					}
				}
                else if (ReadSheetRow(m_workbook.Sheets[sheetIndex]))
                {
                    // Read Sheet Columns
                    //Console.WriteLine("Read Sheet Columns");
                    for (int index = 0; index < m_cellsValues.Length; index++)
                    {
                        if (m_cellsValues[index] != null && m_cellsValues[index].ToString().Length > 0)
                        {
                            table.AddColumnHandleDuplicate(m_cellsValues[index].ToString());
                        }
                        else
                        {
                            table.AddColumnHandleDuplicate(string.Concat(COLUMN, index));
                        }
                    }
                }
                else
                {
                    continue;
                }

                // Read Sheet Rows
                //Console.WriteLine("Read Sheet Rows");
                table.BeginLoadData();
                var sheet = m_workbook.Sheets[sheetIndex];
                //Console.WriteLine("SheetIndex Is:{0},Name:{1}",sheetIndex,sheet.Name);
				while (ReadSheetRow(sheet))
				{
					table.Rows.Add(m_cellsValues);
				}
                if (table.Rows.Count > 0)
                {
                    dataset.Tables.Add(table);
                }

                // Read HyperLinks
                //Console.WriteLine("Read Sheet HyperLinks:{0}",table.Rows.Count);
                ReadHyperLinks(m_workbook.Sheets[sheetIndex],table);

                table.EndLoadData();
			}
            dataset.AcceptChanges();
            dataset.FixDataTypes();
			return dataset;
		}

		public bool IsFirstRowAsColumnNames
		{
			get
			{
				return m_isFirstRowAsColumnNames;
			}
			set
			{
				m_isFirstRowAsColumnNames = value;
			}
		}

		public bool IsValid
		{
			get { return m_isValid; }
		}

		public string ExceptionMessage
		{
			get { return m_exceptionMessage; }
		}

		public string Name
		{
			get
			{
				return (m_resultIndex >= 0 && m_resultIndex < ResultsCount) ? m_workbook.Sheets[m_resultIndex].Name : null;
			}
		}

		public void Close()
		{
			m_isClosed = true;

			if (m_xmlReader != null) m_xmlReader.Close();

			if (m_sheetStream != null) m_sheetStream.Close();

			if (m_zipWorker != null) m_zipWorker.Dispose();
		}

		public int Depth
		{
			get { return m_depth; }
		}

		public int ResultsCount
		{
			get { return m_workbook == null ? -1 : m_workbook.Sheets.Count; }
		}

		public bool IsClosed
		{
			get { return m_isClosed; }
		}

		public bool NextResult()
		{
			if (m_resultIndex >= (this.ResultsCount - 1)) return false;

			m_resultIndex++;

			m_isFirstRead = true;
		    m_savedCellsValues = null;

			return true;
		}

		public bool Read()
		{
			if (!m_isValid) return false;

			if (m_isFirstRead && !InitializeSheetRead())
			{
				return false;
			}

			return ReadSheetRow(m_workbook.Sheets[m_resultIndex]);
		}

		public int FieldCount
		{
			get { return (m_resultIndex >= 0 && m_resultIndex < ResultsCount) ? m_workbook.Sheets[m_resultIndex].ColumnsCount : -1; }
		}

		public bool GetBoolean(int i)
		{
			if (IsDBNull(i)) return false;

			return Boolean.Parse(m_cellsValues[i].ToString());
		}

		public DateTime GetDateTime(int i)
		{
			if (IsDBNull(i)) return DateTime.MinValue;

			try
			{
				return (DateTime)m_cellsValues[i];
			}
			catch (InvalidCastException)
			{
				return DateTime.MinValue;
			}

		}

		public decimal GetDecimal(int i)
		{
			if (IsDBNull(i)) return decimal.MinValue;

			return decimal.Parse(m_cellsValues[i].ToString());
		}

		public double GetDouble(int i)
		{
			if (IsDBNull(i)) return double.MinValue;

			return double.Parse(m_cellsValues[i].ToString());
		}

		public float GetFloat(int i)
		{
			if (IsDBNull(i)) return float.MinValue;

			return float.Parse(m_cellsValues[i].ToString());
		}

		public short GetInt16(int i)
		{
			if (IsDBNull(i)) return short.MinValue;

			return short.Parse(m_cellsValues[i].ToString());
		}

		public int GetInt32(int i)
		{
			if (IsDBNull(i)) return int.MinValue;

			return int.Parse(m_cellsValues[i].ToString());
		}

		public long GetInt64(int i)
		{
			if (IsDBNull(i)) return long.MinValue;

			return long.Parse(m_cellsValues[i].ToString());
		}

		public string GetString(int i)
		{
			if (IsDBNull(i)) return null;

			return m_cellsValues[i].ToString();
		}

		public object GetValue(int i)
		{
			return m_cellsValues[i];
		}

		public bool IsDBNull(int i)
		{
			return (null == m_cellsValues[i]) || (DBNull.Value == m_cellsValues[i]);
		}

		public object this[int i]
		{
			get { return m_cellsValues[i]; }
		}

		#endregion

		#region IDisposable Members

		public void Dispose()
		{
			Dispose(true);

			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			// Check to see if Dispose has already been called.

			if (!this.disposed)
			{
				if (disposing)
				{
					if (m_xmlReader != null) ((IDisposable) m_xmlReader).Dispose();
					if (m_sheetStream != null) m_sheetStream.Dispose();
					if (m_zipWorker != null) m_zipWorker.Dispose();
				}

				m_zipWorker = null;
				m_xmlReader = null;
				m_sheetStream = null;

				m_workbook = null;
				m_cellsValues = null;
				m_savedCellsValues = null;

				disposed = true;
			}
		}

		~ExcelOpenXmlReader()
		{
			Dispose(false);
		}

		#endregion

		#region  Not Supported IDataReader Members


		public DataTable GetSchemaTable()
		{
			throw new NotSupportedException();
		}

		public int RecordsAffected
		{
			get { throw new NotSupportedException(); }
		}

		#endregion

		#region Not Supported IDataRecord Members


		public byte GetByte(int i)
		{
			throw new NotSupportedException();
		}

		public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
		{
			throw new NotSupportedException();
		}

		public char GetChar(int i)
		{
			throw new NotSupportedException();
		}

		public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
		{
			throw new NotSupportedException();
		}

		public IDataReader GetData(int i)
		{
			throw new NotSupportedException();
		}

		public string GetDataTypeName(int i)
		{
			throw new NotSupportedException();
		}

		public Type GetFieldType(int i)
		{
			throw new NotSupportedException();
		}

		public Guid GetGuid(int i)
		{
			throw new NotSupportedException();
		}

		public string GetName(int i)
		{
			throw new NotSupportedException();
		}

		public int GetOrdinal(string name)
		{
			throw new NotSupportedException();
		}

		public int GetValues(object[] values)
		{
			throw new NotSupportedException();
		}

		public object this[string name]
		{
			get { throw new NotSupportedException(); }
		}

		#endregion
	}
}
