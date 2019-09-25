Imports Microsoft.VisualBasic.Net.Http

Module Module1

    Sub Main()
        Dim png$ = "https://files.yande.re/image/59507943242a4e419306ccbec3366cc4/yande.re%2034352%20abra%20aron%20gible%20gloom%20golem%20ho-oh%20hypno%20lotad%20lugia%20luxio%20magby%20minun%20natu%20numel%20onix%20paras%20pichu%20ralts%20riolu%20rotom%20seel%20shinx%20uxie%20xatu%20yanma%20zubat.jpg"

        Call wget.Download(png)
    End Sub

End Module
