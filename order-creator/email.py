
import smtplib, ssl, re
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText
from email.mime.application import MIMEApplication

from email.message import EmailMessage

class Email:
  def __init__(self):
    self._msg = EmailMessage()
    self._recipients = []

  """
  Adds user's login information to the email object
  @param sender: A String sender to indicate who is sending the email
  @param password: A String containing user's password for login
  @raise ValueError: The provided email is in an incorrect format or not in the Golf Discount domain
  """
  def login(self, sender, password):
    if not re.match(r'.+@golfdiscount\.com', sender):
      raise ValueError('The specified email is either in an ' \
        'incorrect format or is not part of the Golf Discount domain.')

    self._sender = sender
    self._msg['Sender'] = sender
    self._password = password
    self._context = ssl.create_default_context()

  """
  Attaches a plain text file to the current email object
  @param attachment: A path-like object to the file to be attached
  @param fileName: Name of the attachment, this is not the same as the file path
    Defaults to 'WSI Orders'
  @raise: Any exception that occurs during file handling and attaching
  """
  def attach(self, attachment, fileName='WSI Orders'):
    try:
      with open(attachment, 'rb') as attachment:
        attach_data = attachment.read()
        self._msg.add_attachment(attach_data, maintype='text', subtype='plain', filename = fileName)
    except Exception as e:
      raise e

  """
  Sends the email message through the specified server and port
  @param server: The mailing server to send the message through
  @param port: The port number the server runs on
  """
  def send_msg(self, server, port):
      with smtplib.SMTP_SSL(server, port, context=self._context) as server:
        self._msg['To'] = ', '.join(self._recipients)
        server.login(self._sender, self._password)
        server.send_message(self._msg)
        server.quit()

  """
  Adds a recipient to the list of recipients
  @param recipient: A String recipient to be added to the list of recipients
  """
  def set_recipient(self, recipient):
    self._recipients.append(recipient)

  """
  Updates the subject line of the message
  @param subject: A string subject of the message
  """
  def set_subject(self, subject):
    self._msg['Subject'] = subject