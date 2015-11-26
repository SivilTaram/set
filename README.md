# set
`set` means `software engineering tools`

# tables
- [x] [Personal Software Process(PSP)](https://github.com/fanfeilong/set/blob/master/doc/SE_PSP.xlsx)
- [x] [SE Mid-Term Survey](https://github.com/fanfeilong/set/blob/master/doc/SE_Mid_Survey.docx)
- [x] [SE Final Skill Survey](https://github.com/fanfeilong/set/blob/master/doc/SE_Final_Skill_survey.xlsx)

# MarkDown Tool
- [x] convert excel to markdown, see [exceltk](https://github.com/fanfeilong/set/tree/master/src/excel)
    - Feature
        - Convert Excel table to MarkDown Table
        - Excel HyperLink cell will be convert To `[text](url)` format 
        - Because MarkDown Dit NOT support Cross Line Cell, Excel's Cross Line cell will be "expand" to Multiline MarkDown table cell 
    - Useage:
        - `exceltk.exe -t md -xls example.xls` 
        - `exceltk.exe -t md -xls example.xls -sheet sheetname`
        - `exceltk.exe -t md -xls example.xlsx` 
        - `exceltk.exe -t md -xls example.xlsx -sheet sheetname`
- [x] gui for exceltk. see: [exceltk gui](https://github.com/fanfeilong/set/tree/master/src/excel/ExceltkGUI)

# C# Linq Scripts For Auto Process
- [-] auto fetch posts of student's cnblog
- [x] plot curver, histogram, pie. see:[plot.linq](https://github.com/fanfeilong/set/tree/master/src/plot/plot.linq)
- [x] add tfidf linq, see:[tfidf.linq](https://github.com/fanfeilong/set/blob/master/src/tfidf/tfidf.linq)

- [x] auto statistic students's self `SE Final Skill Survey` tables, see:
    - [statistic.linq](https://github.com/fanfeilong/set/blob/master/src/assessment/statistic.linq)
- [x] auto statistic students's `SE Mid-Term Survey` documents, see:
    - [se-mid-survey-analizer.linq](https://github.com/fanfeilong/set/blob/master/src/assessment/se-mid-survey-analizer.linq)
