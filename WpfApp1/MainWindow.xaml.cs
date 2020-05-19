using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp1
{
    class phraseDictionary
    {
        private Dictionary<string, Tuple<int, bool, int>> phrases;

        public phraseDictionary()
        {
            this.phrases = new Dictionary<string, Tuple<int, bool, int>>();
        }

        public bool sequenceInDict(string text)
        {
            return phrases.ContainsKey(text);
        }

        public Tuple<int, bool, int> addPhrase(string text, bool symbol)
        {
            if (text == "" && !sequenceInDict(symbol.ToString()))
                phrases[symbol ? "1" : "0"] = new Tuple<int, bool, int>(phrases.Keys.Count + 1, symbol, 0);
            else if (phrases.TryGetValue(text, out var dictPhrase) &&
                     !sequenceInDict(string.Format("{0}{1}", text, symbol ? "1" : "0")))
                phrases[string.Format("{0}{1}", text, symbol ? "1" : "0")] =
                    new Tuple<int, bool, int>(phrases.Count + 1, symbol, dictPhrase.Item1);
            else
            {
                throw new DataException();
            }

            return phrases[string.Format("{0}{1}", text, symbol ? "1" : "0")];
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog1 = new OpenFileDialog
            {
                Filter = "bxb files (*.bxb)|*.bxb"
            }; 

            if (openFileDialog1.ShowDialog().Value)
            {
                var path = openFileDialog1.FileName;
                var res = Undo(path);
                var save = new SaveFileDialog
                {
                    Filter = $"{res.Item2} files(*.{res.Item2}) | *.{res.Item2}"
                };            
                if (save.ShowDialog().Value)
                {
                    var pathSave = save.FileName;
                    File.WriteAllBytes(pathSave, res.Item1);
                }

            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var openFileDialog1 = new OpenFileDialog();
            if (openFileDialog1.ShowDialog().Value)
            {
                var path = openFileDialog1.FileName;
                var res = Arch(path);
                var save = new SaveFileDialog
                {
                    Filter = "bxb files (*.bxb)|*.bxb"
                };
                if (save.ShowDialog().Value)
                {
                    var pathSave = save.FileName;
                    File.WriteAllBytes(pathSave, res);
                }

            }
        }


        private static Tuple<byte[],string> Undo(string path)
        {
            var byteArr = File.ReadAllBytes(path);
            var extLen = (int)((byteArr[0] * 0x0202020202 & 0x010884422010) % 1023);
            var ext = new List<byte>();
            for (var i = 1; i < 1 + extLen; i++)
            {
                ext.Add(byteArr[i]);
            }
            var textExt = string.Join("", ext.Select(x => BitConverter.ToChar(new byte[] { x, 0 })));
            var bitDict = new BitArray(byteArr);
            var dict = new bool[bitDict.Length];
            bitDict.CopyTo(dict, 0);
            var listPhrases = dict.Skip(8 + extLen * 8);
            var offset = 0;
            var result = new StringBuilder();
            var dictionary = new Dictionary<int, string>();
            while (listPhrases.Count() > 5)
            {
                var prevItem = 0;
                offset = ByteToInt(listPhrases.Take(5));
                listPhrases = listPhrases.Skip(5);
                if (offset > 0)
                {
                    prevItem = ByteToInt(listPhrases.Take(offset));
                    listPhrases = listPhrases.Skip(offset);
                }
                var currentVal = listPhrases.Take(1);
                listPhrases = listPhrases.Skip(1);
                if (offset > 0)
                {
                    dictionary.Add(dictionary.Count + 1, dictionary[prevItem] + (currentVal.First() ? "1" : "0"));
                }
                else
                {
                    dictionary.Add(dictionary.Count + 1, currentVal.First() ? "1" : "0");
                }
                result.Append(dictionary[dictionary.Count]);
            }
            var ref1 = result.ToString().Select(x => x == '1').ToArray();
            BitArray a = new BitArray(ref1);
            var bytes = new byte[a.Length / 8 + 1];
            a.CopyTo(bytes, 0);
            return new Tuple<byte[], string>(bytes, textExt);
        }

        public static int ByteToInt(IEnumerable<bool> vs)
        {
            var r = vs.Reverse().ToList();
            var result = 0;
            for (int i = 0; i < vs.Count(); i++)
            {
                result += (r[i] ? 1 : 0) * (int)Math.Pow(2, i);
            }
            return result;
        }

        public static byte[] Arch(string path)
        {
            var byteArr = File.ReadAllBytes(path);
            var bitArr = new BitArray(byteArr);
            var temp = new StringBuilder();
            var phrases = new phraseDictionary();
            var result = new List<bool>();
            for (int i = 0; i < bitArr.Length; i++)
            {
                if (!phrases.sequenceInDict(string.Format("{0}{1}", temp.ToString(), bitArr[i] ? '1' : '0')))
                {
                    result.AddRange(GetBits(phrases.addPhrase(temp.ToString(), bitArr[i])));
                    temp.Clear();
                }
                else
                {
                    temp.Append(bitArr[i] ? 1 : 0);
                }
            }
            var e = BitConverter.GetBytes('t');
            var exst = new BitArray(path.Reverse().TakeWhile(x => x != '.')
                .Reverse().Select(x => BitConverter.GetBytes(x).First()).ToArray());
            var temp1 = new bool[exst.Length];
            exst.CopyTo(temp1, 0);
            var extLenght = (List<bool>)Format(IntToArr(exst.Count / 8), 8);
            extLenght.AddRange(temp1);
            extLenght.AddRange(result.ToArray());
            BitArray a = new BitArray(extLenght.ToArray());
            var bytes = new byte[a.Length / 8 + 1];
            a.CopyTo(bytes, 0);
            return bytes;
        }

        public static IList<bool> GetBits(Tuple<int, bool, int> phrase)
        {
            var bits = IntToArr(phrase.Item3);
            var result = new List<bool>(Format(IntToArr(bits.Count), 5));
            result.AddRange(bits);
            result.Add(phrase.Item2);
            return result;
        }

        public static IList<bool> Format(IList<bool> arr, int mask)
        {
            var result = new bool[mask - arr.Count].ToList();
            result.AddRange(arr);
            return result;
        }

        public static IList<bool> IntToArr(int j)
        {
            var bitIndex = new BitArray(new[] { j });
            var flag = false;
            var result = new List<bool>();
            for (int i = bitIndex.Length - 1; i >= 0; i--)
            {
                if (bitIndex[i] || (!bitIndex[i] && flag))
                {
                    flag = true;
                    result.Add(bitIndex[i]);
                }
            }
            return result;
        }
    }
}