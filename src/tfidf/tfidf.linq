<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Net.Http.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Web.dll</Reference>
  <Reference>&lt;ProgramFilesX86&gt;\Microsoft ASP.NET\ASP.NET MVC 4\Assemblies\System.Web.Http.dll</Reference>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Net.Http.Headers</Namespace>
</Query>

void Main()
{
    // see:https://en.wikipedia.org/wiki/Tf%E2%80%93idf        
    var doc1 = new List<string>(){
        "this",
        "is",
        "a",
        "a",
        "sample"
    };
    
    var doc2 = new List<string>(){
        "this",
        //"this",
        "is",
        "a",
        //"a",
        "sample"
        //"another",
        //"another",
        //"example",
        //"example",
        //"example"
    };
    
    var doc3 = new List<string>(){
        "this",
        "is",
        "a",
        "a",
        "sample"
    };
    
    var doc4 = new List<string>(){
        "this",
        "is",
        "a",
        "two",
        "sample"
    };
    
    var doc5 = new List<string>(){
        "this",
        "is",
        "ccc",
        "sss",
        "sample"
    };
    
    var doc6 = new List<string>(){
        "this",
        "is",
        "ccc",
        "aasss",
        "sample"
    };
    
    var doc7 = new List<string>(){
        "this",
        "is",
        "a",
        "two",
        "sample"
    };
    

    
    var docs = new Dictionary<string,List<string>>{
        {"doc1",doc1},
        {"doc2",doc2},
        {"doc3",doc3},
        {"doc4",doc4},
        {"doc5",doc5},
        {"doc6",doc6},
        {"doc7",doc7}
    };
    
    var fdIdfs = docs.ToFDIDF(2,
        f=>f,
        (N,d)=>1+Math.Log(N*1.0/d,10)
    );    
    
    fdIdfs.Rank().Dump();
}

// Define other methods and classes here
public static class Extension{
    public static List<Tuple<string,List<double>>> ToFDIDF(this Dictionary<string,List<string>> docs,int N,Func<int,int> tfFun,Func<int,int,double> idfFun){
        // count
        var tfs = new Dictionary<string,Dictionary<string,int>>();
        var idfs = new Dictionary<string,int>();
        foreach(var doc in docs){
            var tf = new Dictionary<string,int>(); 
            foreach(var word in doc.Value){
                if(tf.ContainsKey(word)){
                    tf[word]++;
                }else{
                    idfs[word]=idfs.ContainsKey(word)?idfs[word]+1:1;
                    tf[word]=1;                    
                }
            }
            tfs[doc.Key]=tf;
        }
        
        tfs.Dump();
        idfs.Dump();
        
        // calculate
        var tfidfs = new Dictionary<string,List<double>>();
        foreach(var tf in tfs){
            var tfidf = new List<double>();
            var tfDict = tf.Value;
            foreach(var idf in idfs){
                if(tfDict.ContainsKey(idf.Key)){
                    var tfValue = tfFun(tfDict[idf.Key]);
                    var idfValue = idfFun(N,idf.Value);
                    var tfidfValue = tfValue*idfValue;
                    tfidf.Add(tfidfValue);
                }else{
                    tfidf.Add(0);
                }
            }
            tfidfs.Add(tf.Key,tfidf);
        }
        return tfidfs.Dump();
    }
    
    public static double Cos(this List<double> V1, List<double> V2){
        int N = ((V2.Count < V1.Count)?V2.Count : V1.Count);
        double dot  = 0.0d;
        double mag1 = 0.0d;
        double mag2 = 0.0d;
        for (int n = 0; n < N; n++){
            dot += V1[n] * V2[n];
            mag1 += Math.Pow(V1[n], 2);
            mag2 += Math.Pow(V2[n], 2);
        }
        return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
    }
    public static IEnumerable<Tuple<string,double>> Rank(this List<Tuple<string,List<double>>> list){
        var item1 = list[0];
        var query = list.Select(item=>Tuple.Create(item.Item1,item.Item2.Cos(item1.Item2))).OrderBy(v=>v.Item2);
        foreach(var i in query){
            yield return i;
        }
    }
}