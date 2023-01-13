package org.miracum.recruit.notify.mailsender;

import javax.mail.MessagingException;
import javax.mail.internet.MimeMessage;
import org.springframework.mail.javamail.JavaMailSender;
import org.springframework.mail.javamail.MimeMessageHelper;
import org.thymeleaf.TemplateEngine;
import org.thymeleaf.context.Context;

/** Message will be prepared and will be sent by java mail sender and thymeleaf template. */
public class MailSender {
  private final JavaMailSender javaMailSender;
  private final TemplateEngine templateEngine;

  public MailSender(JavaMailSender javaMailSender, TemplateEngine templateEngine) {
    this.javaMailSender = javaMailSender;
    this.templateEngine = templateEngine;
  }

  public void sendMail(NotifyInfo notifyInfo, MailInfo mailInfo) throws MessagingException {
    var mimeMessage = prepareMessage(notifyInfo, mailInfo);
    javaMailSender.send(mimeMessage);
  }

  private MimeMessage prepareMessage(NotifyInfo notifyInfo, MailInfo mailInfo)
      throws MessagingException {
    var mimeMessage = javaMailSender.createMimeMessage();

    var ctx = new Context();
    ctx.setVariable("studyName", notifyInfo.getStudyAcronym());
    ctx.setVariable("screeningListUrl", notifyInfo.getScreeningListLink());

    var textContent = templateEngine.process("notification-mail.txt", ctx);
    var htmlContent = templateEngine.process("notification-mail.html", ctx);

    var messageHelper = new MimeMessageHelper(mimeMessage, true, "UTF-8");
    messageHelper.setSubject(mailInfo.getSubject());
    messageHelper.setFrom(mailInfo.getFrom());
    messageHelper.setTo(mailInfo.getTo());
    messageHelper.setText(textContent, htmlContent);

    return mimeMessage;
  }
}
