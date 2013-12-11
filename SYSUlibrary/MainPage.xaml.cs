using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;
using Microsoft.Phone.Tasks;

namespace SYSUlibrary
{
    public partial class MainPage : PhoneApplicationPage
    {
        public struct BookInfo
        {
            public string title;
            public string author;
            public string publisher;
            public string page;
            public string price;
            public string number;
            public string detail;

            public override string ToString()
            {
                return title + " " + author + " " + publisher +
                    " " + page + " " + price + " " + number + "\n";
            }

            public string Title
            {
                get
                {
                    return title;
                }
            }

            public string Detail
            {
                get
                {
                    return number + " " + author + " " + publisher + " " + page;
                }
            }
        }
        
        
        // Constructor
        public MainPage()
        {
            InitializeComponent();

            // Set the data context of the listbox control to the sample data
            DataContext = App.ViewModel;
            this.Loaded += new RoutedEventHandler(MainPage_Loaded);
            SecondListBox.ItemsSource = books;
            string helpinfo = "    这是一个基于WP7的中山大学图书馆查询客户端，可以方便查询到图书馆的现有藏书及索取号码等信息。因为作者的偷懒，所以限定了只显示前40个搜索信息（如果有的话），不过一般来说，也够了吧。\n" +
                "    今后会加上的功能有图书的馆藏情况，手机续借，还书提醒等等，收藏和结合豆瓣的评论系统也会后序加上。另外可能会抓取当当、亚马逊上的一些书评，方便大家选书，也会搭建一个自己的服务器用来收集大家的评论。\n"+
                "    希望在大家的努力下建成一个真正的数字化中大！\n\n" + 
                "    点击图片可以访问我的微博，欢迎关注，哈哈";
            help.Text = helpinfo;

            this.BackKeyPress += new EventHandler<System.ComponentModel.CancelEventArgs>(MainPage_BackKeyPress);
        }

        void MainPage_BackKeyPress(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MessageBoxResult msgRst = MessageBox.Show("要退出本程序吗？", "提示", MessageBoxButton.OKCancel);
            if (msgRst == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
            }
            
        }


        // Load data for the ViewModel Items
        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!App.ViewModel.IsDataLoaded)
            {
                App.ViewModel.LoadData();
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            //contentStr = "v_index=TITLE&v_value=" + textBox1.Text.ToString() + "&v_pagenum=10&v_seldatabase=0&v_LogicSrch=0&FLD_DAT_BAG=&FLD_DAT_END=&submit=查&nbsp;询";
            contentStr = "v_index=TITLE&v_value="+ textBox1.Text + "&v_pagenum=40&v_seldatabase=0&v_LogicSrch=0&FLD_DAT_BAG=&FLD_DAT_END=&submit=查&nbsp;询";
            post("http://202.116.65.88/cgi-bin/IlaswebBib");
            Message.Text = "正在查找中.....";
            if (books.Count > 0)
                books.Clear();
        }



        private delegate void AddDelegate(BookInfo b);
        private void Add(BookInfo b)
        {
            if (pivot.SelectedItem != pitem2)
                pivot.SelectedItem = pitem2;
            books.Add(b);
        }


        void Set()
        {
            if (!resultStr.Equals("成功"))
                Message.Text = resultStr;
        }

        #region HTTP section

        public String UrlStr;
        private HttpWebRequest httpRequest;
        private HttpWebResponse httpResponse;
        private CookieContainer cc = new CookieContainer();

        private String resultStr;
        String contentStr;

        // 向服务器发送POST请求，用于登录
        private void post(String baseUrl)
        {
            try
            {
                this.UrlStr = baseUrl;
                httpRequest = (HttpWebRequest)WebRequest.Create(UrlStr);

                httpRequest.Method = "POST";
                httpRequest.Accept = "text/html";
                httpRequest.ContentType = "application/x-www-form-urlencoded";
                httpRequest.CookieContainer = cc;
                httpRequest.Headers[HttpRequestHeader.KeepAlive] = "true";
                httpRequest.BeginGetRequestStream(new AsyncCallback(RequestCallBack), httpRequest);
            }
            catch (Exception e)
            {
                resultStr = e.Message;
            }

        }

        // post函数的回调函数
        void RequestCallBack(IAsyncResult result)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)result.AsyncState;

                Stream postStream = request.EndGetRequestStream(result);
                //byte[] argsBytes = Encoding.UTF8.GetBytes(contentStr);
                Encoding ec = new GB2312Encoding();

                byte[] argsBytes = ec.GetBytes(contentStr);
                // Add the post data to the web request
                postStream.Write(argsBytes, 0, argsBytes.Length);
                postStream.Close();

                request.BeginGetResponse(new AsyncCallback(ResponseCallBack), request);
            }
            catch (WebException e)
            {
                resultStr = e.Message;
            }
        }

        // 回调函数的回调函数
        void ResponseCallBack(IAsyncResult result)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)result.AsyncState;
                httpResponse = (HttpWebResponse)request.EndGetResponse(result);
                Encoding ec = new GB2312Encoding();
                StreamReader readerStream = new StreamReader(httpResponse.GetResponseStream(), ec);
                resultStr = readerStream.ReadToEnd();
                
                resultStr = stringToBookinfo(ref resultStr);
                // 需要处理一下

                Dispatcher.BeginInvoke(Set);

                readerStream.Close();

            }
            catch (WebException e)
            {
                resultStr = e.Message;
            }

        }


        #endregion
        
        private ObservableCollection<BookInfo> books = new ObservableCollection<BookInfo>();
        
        private string stringToBookinfo(ref string Htmlstring)
        {
            int total, start, end, page, curS, curE;



            start = Htmlstring.IndexOf("命中约");
            end = Htmlstring.IndexOf("条记录");

            if (start < 0)
            {
                return "没有找到相关书籍";
            }

            Regex rg = new Regex("详细信息", RegexOptions.IgnoreCase);
            MatchCollection mc = rg.Matches(Htmlstring);

            page = mc.Count - 1;
            total = Convert.ToInt32(Htmlstring.Substring(start + 22, end - start - 29));



            BookInfo[] test = new BookInfo[page];
            int temp_index = 0;


            curS = Htmlstring.IndexOf("详细信息", temp_index);
            curE = Htmlstring.IndexOf(@"</td>", curS + 20);

            test[0].title = Htmlstring.Substring(curS + 68, curE - curS - 68);
            test[0].title = test[0].title.Replace("\n", "");
            curS = curE;
            curE = Htmlstring.IndexOf(@"</td>", curS + 20);
            test[0].author = Htmlstring.Substring(curS + 49, curE - curS - 49);
            test[0].author = test[0].author.Replace("\n", "");
            test[0].author = test[0].author.Replace("&nbsp;", "");
            curS = curE;
            curE = Htmlstring.IndexOf(@"</td>", curS + 20);
            test[0].publisher = Htmlstring.Substring(curS + 49, curE - curS - 49);
            test[0].publisher = test[0].publisher.Replace("\n", "");
            test[0].publisher = test[0].author.Replace("&nbsp;", "");
            curS = curE;
            curE = Htmlstring.IndexOf(@"</td>", curS + 20);
            test[0].page = Htmlstring.Substring(curS + 49, curE - curS - 49);
            test[0].page = test[0].page.Replace("\n", "");
            test[0].page = test[0].page.Replace("&nbsp;", "");
            curS = curE;
            curE = Htmlstring.IndexOf(@"</td>", curS + 20);
            test[0].price = Htmlstring.Substring(curS + 49, curE - curS - 49);
            test[0].price = test[0].price.Replace("\n", "");
            test[0].price = test[0].price.Replace("&nbsp;", "");
            curS = curE;
            curE = Htmlstring.IndexOf(@"</td>", curS + 20);
            test[0].number = Htmlstring.Substring(curS + 49, curE - curS - 49);
            test[0].number = test[0].number.Replace("\n", "");
            test[0].number = test[0].number.Replace("&nbsp;", "");
            curS = Htmlstring.IndexOf("href=", curE);
            curE = Htmlstring.IndexOf("\">", curS);
            test[0].detail = Htmlstring.Substring(curS + 6, curE - curS - 6);
            test[0].detail = test[0].detail.Replace("\n", "");
            test[0].detail = test[0].detail.Replace("&nbsp;", "");
            temp_index = curE;

            Dispatcher.BeginInvoke(new AddDelegate(Add), test[0]);
           
            
            for (int i = 1; i < page; i++)
            {
                curS = Htmlstring.IndexOf("详细信息", temp_index);
                curE = Htmlstring.IndexOf(@"</td>", curS + 20);

                test[i].title = Htmlstring.Substring(curS + 66, curE - curS - 66);
                test[i].title = test[i].title.Replace("\n", "");
                curS = curE;
                curE = Htmlstring.IndexOf(@"</td>", curS + 20);
                test[i].author = Htmlstring.Substring(curS + 49, curE - curS - 49);
                test[i].author = test[i].author.Replace("\n", "");
                test[i].author = test[i].author.Replace("&nbsp;", "");
                curS = curE;
                curE = Htmlstring.IndexOf(@"</td>", curS + 20);
                test[i].publisher = Htmlstring.Substring(curS + 49, curE - curS - 49);
                test[i].publisher = test[i].publisher.Replace("\n", "");
                test[i].publisher = test[i].publisher.Replace("&nbsp;", "");
                curS = curE;
                curE = Htmlstring.IndexOf(@"</td>", curS + 20);
                test[i].page = Htmlstring.Substring(curS + 49, curE - curS - 49);
                test[i].page = test[i].page.Replace("\n", "");
                test[i].page = test[i].page.Replace("&nbsp;", "");
                curS = curE;
                curE = Htmlstring.IndexOf(@"</td>", curS + 20);
                test[i].price = Htmlstring.Substring(curS + 49, curE - curS - 49);
                test[i].price = test[i].price.Replace("\n", "");
                test[i].price = test[i].price.Replace("&nbsp;", "");
                curS = curE;
                curE = Htmlstring.IndexOf(@"</td>", curS + 20);
                test[i].number = Htmlstring.Substring(curS + 49, curE - curS - 49);
                test[i].number = test[i].number.Replace("\n", "");
                test[i].number = test[i].number.Replace("&nbsp;", "");
                curS = Htmlstring.IndexOf("href=", curE);
                curE = Htmlstring.IndexOf("\">", curS);
                test[i].detail = Htmlstring.Substring(curS + 6, curE - curS - 6);
                test[i].detail = test[i].detail.Replace("\n", "");
                test[i].detail = test[i].detail.Replace("&nbsp;", "");
                temp_index = curE;

                Dispatcher.BeginInvoke(new AddDelegate(Add), test[i]);
            }
            
            return "成功";
        }

        private void image2_Tap(object sender, GestureEventArgs e)
        {
            WebBrowserTask task = new WebBrowserTask();
            task.Uri = new Uri("http://weibo.com/wdxtub");
            task.Show();
        }
        
    }
}