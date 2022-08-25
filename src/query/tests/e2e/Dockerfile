FROM python:3.10-alpine

# hadolint ignore=DL3018
RUN apk --no-cache add git

WORKDIR /opt/tests
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY . .

ENTRYPOINT ["pytest"]
CMD [ "test.py" ]
