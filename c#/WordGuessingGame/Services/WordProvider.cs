
using System.Collections.Generic;
using System;
using WordGuessingGame.Interfaces;

namespace WordGuessingGame.Services
{

    class WordProvider: IWordProvider
    {
        private List<string> _words = new List<string>
        {
            "APPLE","MANGO","GRAPE","TRAIN","PLANT","BRAIN"
        };
        public string GetRandomWord()
        {
            Random random = new Random();
            int index = random.Next(_words.Count);
            return _words[index];
        }
    }
}