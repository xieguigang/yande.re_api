Imports System.Xml.Serialization
Imports Microsoft.VisualBasic.ApplicationServices.Debugging

Namespace Models

    <XmlRoot("posts")> Public Class Posts

        <XmlAttribute> Public Property count As Integer
        <XmlAttribute> Public Property offset As Integer

        <XmlElement("post")>
        Public Property posts As post()

    End Class

    Public Class post : Implements IVisualStudioPreviews

        Private ReadOnly Property Previews As String Implements IVisualStudioPreviews.Previews
            Get
                Return (<img src=<%= preview_url %> style="width:100%; height:100%;"/>).ToString
            End Get
        End Property

        <XmlAttribute> Public Property id As String
        <XmlAttribute> Public Property tags As String
        <XmlAttribute> Public Property created_at As String
        <XmlAttribute> Public Property updated_at As String
        <XmlAttribute> Public Property creator_id As String
        <XmlAttribute> Public Property approver_id As String
        <XmlAttribute> Public Property author As String
        <XmlAttribute> Public Property change As String
        <XmlAttribute> Public Property source As String
        <XmlAttribute> Public Property score As String
        <XmlAttribute> Public Property md5 As String
        <XmlAttribute> Public Property file_size As String
        <XmlAttribute> Public Property file_ext As String
        <XmlAttribute> Public Property file_url As String
        <XmlAttribute> Public Property is_shown_in_index As String
        <XmlAttribute> Public Property preview_url As String
        <XmlAttribute> Public Property preview_width As String
        <XmlAttribute> Public Property preview_height As String
        <XmlAttribute> Public Property actual_preview_width As String
        <XmlAttribute> Public Property actual_preview_height As String
        <XmlAttribute> Public Property sample_url As String
        <XmlAttribute> Public Property sample_width As String
        <XmlAttribute> Public Property sample_height As String
        <XmlAttribute> Public Property sample_file_size As String
        <XmlAttribute> Public Property jpeg_url As String
        <XmlAttribute> Public Property jpeg_width As String
        <XmlAttribute> Public Property jpeg_height As String
        <XmlAttribute> Public Property jpeg_file_size As String
        <XmlAttribute> Public Property rating As String
        <XmlAttribute> Public Property is_rating_locked As String
        <XmlAttribute> Public Property has_children As String
        <XmlAttribute> Public Property parent_id As String
        <XmlAttribute> Public Property status As String
        <XmlAttribute> Public Property is_pending As String
        <XmlAttribute> Public Property width As String
        <XmlAttribute> Public Property height As String
        <XmlAttribute> Public Property is_held As String
        <XmlAttribute> Public Property frames_pending_string As String
        <XmlAttribute> Public Property frames_string As String
        <XmlAttribute> Public Property is_note_locked As String
        <XmlAttribute> Public Property last_noted_at As String
        <XmlAttribute> Public Property last_commented_at As String

        Public Property flagDetail As flagDetail

    End Class

    <XmlType("flag-detail")> Public Class flagDetail

        <XmlAttribute> Public Property post_id As String
        <XmlAttribute> Public Property reason As String
        <XmlAttribute> Public Property created_at As String
        <XmlAttribute> Public Property user_id As String
        <XmlAttribute> Public Property flagged_by As String

    End Class
End Namespace