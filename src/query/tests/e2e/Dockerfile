FROM docker.io/library/python:3.12.3-alpine@sha256:32385e61c3414ffa5a6dbf52feace89f758ad68709a48d376d56a0232162665a
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
