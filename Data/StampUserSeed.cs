namespace HALRouteCardApp.Data;

public sealed record StampUserSeed(int SerialNumber, int UserNumber, string DisplayName, string[] StampValues);

public static class StampUserSeeds
{
    private const string Rows = """
        V.N Anil Kumar|F1
        N Kunwar Alok|F2
        Raja Shekar|F3
        M. Shanmugam|F4
        D.K.N.C Patel|F5
        D. Rama Shetty|F6
        R. Srinivasa|F7
        R. Raghavendra|F8
        S. Sunish Kumar|F9
        G.S Nanjunda Swamy|F10
        Amit Srivatsa|F11
        T. Rama Chandraiah|F12
        Jolly Kutty|F13
        S. Subash|F14
        R. Ramachandran Nair|F15
        V. Venkateshappa|F16
        Irshad Ansari|F17
        V. Anirudhan|F18
        H. Nagaraja|F19
        P. V Prabhakaran|F20
        R. Manoharan|F21,F104
        K. Karigiri Rao|F22
        M. Teli Shiva Kumar|F23
        S. Ramesh Kumar|F24
        Rabi Shankar Tripathi|F25
        D. Subrahmanya Shastry|F26
        R. Mathivanan|F27
        V. Ananda Kumar|F28
        G. Rajesh|F29
        S.P Ravindranathan|F30
        M.H Shankar|F54,F31
        V. Srinivasaiah|F32
        Sivadasan|F33
        Pandurang Goudar|F34,F128
        E. Sreenath|F35
        SathyaNarayana|F36,F102
        Yajnapal|F37
        Stayendra kumar|F38,F103
        S. Venkat Kishore|F39
        Daitary sethi|F40
        S.B Deshbhratar|F24
        C.K.N.V Sunyanarayana|F25
        G. Ravi Krishna|F54
        Dasharathi|F2,F93
        A. Naga Kishore|F61
        Deepak|F62
        Rachana Misra|F63
        A. Vasanta kumar|F64;G. Ravi Krishna|F63,F76
        Piyoosh.P|F65
        Yesu Babu .B|F66,F129
        Kamal Kumar|F67,F120
        Manoj Kumar|F68
        M. Venkata Rajesh|F69,F77
        K. Amarendranath|F70
        Rajiv Datt|F73
        K. Ravindar|F71,F107
        T. Pradhan|F72
        Praveen B|F80
        S. P Madavan|F75,F88,F101,F108
        Prashanth B.M|F74
        Anzarul Hasan|F85
        Ramesh|F82
        Ningapahalli|F79
        M. Nirmal Kumar|F78,F110
        Shankar Narayan|F83
        Gyan Anshu|F81,F131
        Manoj Kumar|F95
        Piyoosh.P|F96
        Prasad|F97
        Puneeth Kumar B|F84
        Durai Kannan. G|F105
        Dileep Yashwanth P|F94
        S.P Madavan|F101
        Ankit Singh Kashia|F98
        P. Sathish babu|F99
        Nagaraj M.T|F91
        Satyendra Kumar|F103
        J. Rodrigues|F121
        R. Mathivanan|F100
        Vijay Raj|F130
        G.Gangaiah|F125
        K.C Mohanta|F90
        P. Sathish Babu|F92
        N. Perianambi|F123
        Ariventhi. K|F124
        S.P Madavan|F108
        Chethanand G.P|F109
        Yasar K.A|F106
        S.Y Meghashyam|F111
        P.D Aswath|F112
        Vikas Yadav|F114
        Amit Sharma|F117
        VR Anitha|F119;K. Manjunath Reddy|F115
        Ishan Arya|F118
        Abnes Herbert|F113
        Jitendra|F116
        Vijay babu Y|F95
        M. Karthik Reddy|F122
        Mahendra|F126
        GauravMittal|F140,0123 FDB
        Swapnil kumar Chandel|F133
        Mohit Sehgal|F132
        Suresh K.V|F134
        Navin Kumar|F135
        Kathula Subash|F127
        R. Balaji|F130
        Ashok . K|F110
        V. Gireesha|F96
        Ariventhi.K|F136
        A. Srinivasan|F115
        Nataraja K.G|F137
        Venkatesha VM|F138
        D. Raju|F139
        Vinay B.K|F140
        David Paul|F98
        Govinda|F99
        Praveen Kumar Kurri|F92
        M. Lakshmisha|F119
        """;

    public static IReadOnlyList<StampUserSeed> All { get; } = ParseRows();

    private static IReadOnlyList<StampUserSeed> ParseRows()
    {
        var rows = Rows.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var seeds = new List<StampUserSeed>();
        for (var index = 0; index < rows.Length; index++)
        {
            var users = rows[index].Split(';', StringSplitOptions.TrimEntries);
            for (var userIndex = 0; userIndex < users.Length; userIndex++)
            {
                var fields = users[userIndex].Split('|', 2, StringSplitOptions.TrimEntries);
                if (fields.Length != 2 || string.IsNullOrWhiteSpace(fields[0]) || string.IsNullOrWhiteSpace(fields[1]))
                    throw new InvalidOperationException($"Invalid stamp user seed at serial number {index + 1}.");

                var stamps = fields[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                seeds.Add(new StampUserSeed(index + 1, userIndex + 1, fields[0], stamps));
            }
        }

        if (rows.Length != 118)
            throw new InvalidOperationException($"Expected 118 serial-number rows but found {rows.Length}.");

        return seeds;
    }
}
