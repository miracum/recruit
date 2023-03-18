FROM docker.io/library/python:3.11.2-alpine@sha256:741e650697a506f0991ef88490320dee59f9e68de61734e034aee11d2f3baedf
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
