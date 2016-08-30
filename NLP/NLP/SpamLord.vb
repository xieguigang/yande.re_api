Imports System.Text.RegularExpressions
Imports Microsoft.VisualBasic.Language.Python
Imports Microsoft.VisualBasic

''' <summary>
''' 
''' </summary>
''' <remarks>
''' complex case as follows:
'''
''' ```html
''' &lt;td class="value">ouster (followed by &amp;ldquo;@cs.stanford.edu&amp;rdquo;)&lt;/td>
''' <script> obfuscate('stanford.edu','jurafsky'); </script>
''' &lt;td class="value">teresa.lynn (followed by "@stanford.edu")&lt;/td>
''' &lt;dd>	<em>melissa&#x40;graphics.stanford.edu</em>
''' <address>engler WHERE stanford DOM edu</address>
''' email: pal at cs stanford edu,
''' d-l-w-h-@-s-t-a-n-f-o-r-d-.-e-d-u
''' &lt;dd>	<em>ada&#x40;graphics.stanford.edu</em>
''' Email: uma at cs dot stanford dot edu                                                 
''' hager at cs dot jhu dot edu  
''' funding at Stanford comes    
''' (Fedora) Server at cs.stanford.edu Port 80                                                     
''' ```
''' </remarks>
Public Class SpamLord

    ''' <summary>
    ''' pattern for email
    ''' </summary>
    Const my_email_pattern As String = "
                                                   # pattern for email
        (([\w-]+|[\w-]+\.[\w-]+)                   # hanks, justin.hanks, hanks-, justin-hanks-
        (\s.?\(f.*y.*)?                            # followed by 
        (\s?(@|&.*;)\s?|\s(at|where)\s)            # @, @ , at , where ,&#x40;,
        ([\w-]+|[\w-]+([\.;]|\sdo?t\s|\s)[\w-]+)   # gmail., ics.bjtu, ics;bjtu, ics dot bjtu, -ics-bjtu-
        ([\.;]|\s(do?t|DOM)\s|\s)                  # ., ;, dot , dt , DOM
        (-?e-?d-?u|com)\b)                         # .edu, .com, -e-d-u
        |
        (obfuscate\('(\w+\.edu)','(\w+)'\))        # obfuscate('stanford.edu','jurafsky')             
"

    ''' <summary>
    ''' pattern for phone
    ''' </summary>
    Const my_phone_pattern As String = "

                         # pattern for phone
        \(?(\d{3})\)?    # area code Is 3 digits, e.g. (650), 650
        [ -]?            # separator Is - Or space Or nothing, e.g. 650-XXX, 650 XXX, (650)XXX
        (\d{3})          # trunk Is 3 digits, e.g. 800
        [ -]             # separator Is - Or space
        (\d{4})          # rest of number Is 4 digits, e.g. 0987
        \D+              # should have at least one non digit character at the end
"

    Public Function process_file(name As String, f As IEnumerable(Of String)) As String()
        ' note that debug info should be printed To stderr
        ' sys.stderr.write('[process_file]\tprocessing file: %s\n' % (path))
        Dim Res As New List(Of String)
        For Each line As String In f
            ' match email
            Dim matches = Regexp.FindAll(my_email_pattern, line, RegexICSng)
            For Each m As String In matches
                Dim email = ""
                If Len(m.Last [-1])!= 0 Then
                    email = '%s@%s' % (m[-1], m[-2])
            Else
    If m(1) = "Server" Then
                        ' skip "server at" sentence
                        Continue For
                        email = '%s@%s.%s' % (m[1].replace("-", ""), 
                                              m[6].replace(";", ".")
                                          .replace(" dot ", ".")
                    .replace("-", "")
                    .replace(" ", "."), 
                                      m[-4].replace("-", ""))
            Res.Append((name,'e',email))
Next
            ' match phone number
            matches = Regexp.FindAll(my_phone_pattern, line)
            For Each m In matches
                phone = '%s-%s-%s' % m
            Res.Append((name, 'p', phone))
            Next
        Next
            Return Res
End Function
End Class
