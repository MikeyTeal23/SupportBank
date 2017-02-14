﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace SupportBank.ConsoleApp
{
  class Program
  {
    private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

    private enum CommandType
    {
      ListAll,
      ListOne
    }

    private struct Command
    {
      public CommandType Type { get; set; }
      public string Target { get; set; }
    }

    static void Main()
    {
      ConfigureNLog();
      logger.Info("SupportBank starting up");

      var transactions = ReadCSV(@"Transactions2014.csv")
        .Union(ReadCSV(@"DodgyTransactions2015.csv"));
      var accounts = CreateAccountsFromTransactions(transactions);

      PrintWelcomeBanner();

      while (true)
      {
        var command = PromptForCommand();

        switch (command.Type)
        {
          case CommandType.ListAll:
            ListAllAccounts(accounts);
            break;

          case CommandType.ListOne:
            ListOneAccount(accounts[command.Target]);
            break;
        }
      }
    }

    private static void ConfigureNLog()
    {
      var config = new LoggingConfiguration();
      var target = new FileTarget
      {
        FileName = @"SupportBank.log",
        Layout = @"${longdate} ${level} - ${logger}: ${message}"
      };
      config.AddTarget("File Logger", target);
      config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, target));
      LogManager.Configuration = config;
    }

    private static IEnumerable<Transaction> ReadCSV(string filename)
    {
      logger.Info($"Loading transactions from file {filename}");

      var lines = File.ReadAllLines(filename).Skip(1);

      foreach (var line in lines)
      {
        logger.Debug($"Parsing transaction: {line}");

        var fields = line.Split(',');

        yield return new Transaction
        {
          Date = DateTime.Parse(fields[0]),
          From = fields[1],
          To = fields[2],
          Narrative = fields[3],
          Amount = decimal.Parse(fields[4])
        };
      }
    }

    private static Dictionary<string, Account> CreateAccountsFromTransactions(IEnumerable<Transaction> transactions)
    {
      var accounts = new Dictionary<string, Account>();

      foreach (var transaction in transactions)
      {
        GetOrCreateAccount(accounts, transaction.From).OutgoingTransactions.Add(transaction);
        GetOrCreateAccount(accounts, transaction.To).IncomingTransactions.Add(transaction);
      }

      return accounts;
    }

    private static Account GetOrCreateAccount(Dictionary<string, Account> accounts, string owner)
    {
      if (accounts.ContainsKey(owner))
      {
        return accounts[owner];
      }

      logger.Debug($"Adding account for {owner}");
      var newAccount = new Account(owner);
      accounts[owner] = newAccount;
      return newAccount;
    }

    private static void PrintWelcomeBanner()
    {
      Console.WriteLine("Welcome to SupportBank!");
      Console.WriteLine("=======================");
      Console.WriteLine();
      Console.WriteLine("Available commands:");
      Console.WriteLine("  List All - list all account balances");
      Console.WriteLine("  List [Account] - list transactions for the specified account");
      Console.WriteLine();
    }

    private static Command PromptForCommand()
    {
      while (true)
      {
        Console.Write("Your command> ");
        string commandText = Console.ReadLine();

        Command command;

        if (ParseCommand(commandText, out command))
        {
          return command;
        }

        Console.WriteLine("Sorry, I didn't understand that");
        Console.WriteLine();
      }
    }

    private static bool ParseCommand(string commandText, out Command command)
    {
      command = new Command();

      if (!commandText.StartsWith("List "))
      {
        return false;
      }

      if (commandText.Substring(5) == "All")
      {
        command.Type = CommandType.ListAll;
      }
      else
      {
        command.Type = CommandType.ListOne;
        command.Target = commandText.Substring(5);
      }

      return true;
    }

    private static void ListAllAccounts(Dictionary<string, Account> accounts)
    {
      Console.WriteLine("All accounts");

      foreach (var account in accounts.Values)
      {
        var balance = account.IncomingTransactions.Sum(tx => tx.Amount) -
                      account.OutgoingTransactions.Sum(tx => tx.Amount);

        Console.WriteLine($"  {account.Owner} {(balance < 0 ? "owes" : "is owed")} {Math.Abs(balance):C}");
      }

      Console.WriteLine();
    }

    private static void ListOneAccount(Account account)
    {
      Console.WriteLine($"Account {account.Owner}");

      foreach (var transaction in
        account.IncomingTransactions.Union(account.OutgoingTransactions).OrderBy(tx => tx.Date))
      {
        Console.WriteLine(
          $"  {transaction.Date:d}: {transaction.From} paid {transaction.To} {transaction.Amount:C} for {transaction.Narrative}");
      }

      Console.WriteLine();
    }


  }
}
