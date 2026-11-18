// Các test dùng chung DB SmartHealthcare_Test và có khóa phạm vi (UPDLOCK, HOLDLOCK): một collection cho cả assembly để chạy tuần tự.
[assembly: Xunit.CollectionBehavior(Xunit.CollectionBehavior.CollectionPerAssembly)]
